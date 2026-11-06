using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>
/// Phase 5: analytics and the invoice browser. The invoices are produced through the real checkout
/// service so the reports are asserted against figures the till actually wrote.
/// </summary>
public sealed class ReportServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database;
    private readonly IDateTimeProvider _clock;

    public ReportServiceTests()
    {
        _database = new SqliteTestDatabase();
        _clock = new FixedClock(DateTime.UtcNow);
    }

    private SalesService CreateSalesService() => new(
        _database.Context,
        new StockService(_database.Context, TestServices.Logger<StockService>()),
        new NumberSequenceService(new TestContextFactory(_database), _clock),
        new StoreSettingsService(new TestContextFactory(_database)),
        _clock,
        TestServices.Logger<SalesService>());

    private ReportService CreateReportService() =>
        new(_database.Context, _clock);

    private async Task<int> SellAsync(decimal quantity = 1m, PaymentMethod method = PaymentMethod.Cash, int userId = 2)
    {
        var product = await _database.Context.Products.FirstAsync(p => !p.IsWeighted && p.StockQuantity > 20m);

        // Credit sales need a real customer: the till creates one inline, the test seeds it up front.
        var customer = method == PaymentMethod.Credit
            ? await EnsureCustomerAsync()
            : null;

        var result = await CreateSalesService().CompleteSaleAsync(new SaleRequestDto
        {
            UserId = userId,
            Lines = new[]
            {
                new CartLineDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = quantity,
                    UnitPrice = product.SellingPrice,
                    UnitCost = product.CostPrice,
                    VatRate = product.VatRate
                }
            },
            PaidAmount = method == PaymentMethod.Credit ? 0m : 10_000m,
            PaymentMethod = method,
            CustomerId = customer?.Id,
            CustomerName = customer?.Name
        });

        return result.InvoiceId;
    }

    [Fact]
    public async Task InvoiceBrowser_SmallPagesReportTheRemainingPages()
    {
        for (var i = 0; i < 5; i++)
        {
            await SellAsync(1m);
        }

        var page = await CreateReportService().GetInvoicesAsync(new InvoiceFilter
        {
            FromDate = DateTime.Today,
            ToDate = DateTime.Today,
            PageNumber = 2,
            PageSize = 2
        });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
    }

    [Fact]
    public async Task Analytics_SumsTheKPIsOfThePeriod()
    {
        await SellAsync(1m, PaymentMethod.Cash);
        await SellAsync(2m, PaymentMethod.Card);

        var today = DateTime.Today;
        var analytics = await CreateReportService().GetSalesAnalyticsAsync(new ReportFilter
        {
            FromDate = today,
            ToDate = today
        });

        var expected = await _database.Context.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Completed)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        Assert.Equal(2, analytics.InvoiceCount);
        Assert.Equal(expected, analytics.NetSales);
        Assert.True(analytics.TotalProfit > 0m);
        Assert.True(analytics.TotalQuantity > 0m);

        // One bucket per day of the period, zeros included.
        Assert.Single(analytics.DailySales);
        Assert.Equal(today, analytics.DailySales[0].Date);
        Assert.Equal(expected, analytics.DailySales[0].Sales);

        // Both tenders show up in the split.
        Assert.Contains(analytics.PaymentBreakdown, p => p.Method == PaymentMethod.Cash && p.Amount > 0m);
        Assert.Contains(analytics.PaymentBreakdown, p => p.Method == PaymentMethod.Card && p.Amount > 0m);
    }

    [Fact]
    public async Task Analytics_IgnoresInvoicesOutsideThePeriod()
    {
        await SellAsync();

        var yesterday = DateTime.Today.AddDays(-1);
        var analytics = await CreateReportService().GetSalesAnalyticsAsync(new ReportFilter
        {
            FromDate = yesterday,
            ToDate = yesterday
        });

        Assert.Equal(0, analytics.InvoiceCount);
        Assert.Equal(0m, analytics.NetSales);
        Assert.Single(analytics.DailySales);          // the empty day is still on the axis
        Assert.Equal(0m, analytics.DailySales[0].Sales);
    }

    [Fact]
    public async Task Analytics_FiltersByCashierAndComputesTheGrowth()
    {
        await SellAsync(1m, PaymentMethod.Cash, userId: 2);

        var today = DateTime.Today;
        var cashierTwo = await CreateReportService().GetSalesAnalyticsAsync(new ReportFilter
        {
            FromDate = today,
            ToDate = today,
            UserId = 2
        });

        var cashierThree = await CreateReportService().GetSalesAnalyticsAsync(new ReportFilter
        {
            FromDate = today,
            ToDate = today,
            UserId = 3
        });

        Assert.Equal(1, cashierTwo.InvoiceCount);
        Assert.Equal(0, cashierThree.InvoiceCount);

        // No takings in the previous period: growth is reported as 100% for a positive period.
        Assert.Equal(0m, cashierTwo.PreviousPeriodSales);
        Assert.Equal(100m, cashierTwo.SalesGrowth);

        Assert.Single(cashierTwo.Cashiers);
        Assert.Equal(2, cashierTwo.Cashiers[0].UserId);
        Assert.Equal(cashierTwo.NetSales, cashierTwo.Cashiers[0].Sales);
    }

    [Fact]
    public async Task Analytics_BreaksTheSalesDownByCategoryAndProduct()
    {
        var product = await _database.Context.Products.AsNoTracking().FirstAsync(p => !p.IsWeighted && p.StockQuantity > 20m);
        await SellAsync(3m);

        var today = DateTime.Today;
        var analytics = await CreateReportService().GetSalesAnalyticsAsync(new ReportFilter { FromDate = today, ToDate = today });

        Assert.Single(analytics.CategorySales);
        Assert.Equal(3m, analytics.CategorySales[0].Quantity);

        Assert.Single(analytics.TopProducts);
        Assert.Equal(product.Id, analytics.TopProducts[0].ProductId);
        Assert.Equal(3m, analytics.TopProducts[0].QuantitySold);
        Assert.True(analytics.TopProducts[0].Revenue > 0m);

        // The stock table always describes the whole catalog, not just the period.
        Assert.Equal(8, analytics.CategoryStock.Count);
        Assert.All(analytics.CategoryStock, c => Assert.True(c.StockValue >= 0m));
    }

    [Fact]
    public async Task InvoiceBrowser_PagesFiltersAndLoadsTheDetail()
    {
        var first = await SellAsync(1m, PaymentMethod.Cash);
        await SellAsync(1m, PaymentMethod.Credit);

        var today = DateTime.Today;
        var reports = CreateReportService();

        var page = await reports.GetInvoicesAsync(new InvoiceFilter { FromDate = today, ToDate = today, PageSize = 50 });
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(1, page.TotalPages);
        Assert.False(page.HasNext);

        var creditOnly = await reports.GetInvoicesAsync(new InvoiceFilter
        {
            FromDate = today,
            ToDate = today,
            PaymentMethod = PaymentMethod.Credit
        });

        Assert.Single(creditOnly.Items);
        Assert.True(creditOnly.Items[0].IsCredit);
        Assert.Equal("آجل", creditOnly.Items[0].PaymentMethodName);

        var byNumber = await reports.GetInvoicesAsync(new InvoiceFilter
        {
            FromDate = today,
            ToDate = today,
            SearchTerm = creditOnly.Items[0].InvoiceNumber
        });

        Assert.Single(byNumber.Items);

        var detail = await reports.GetInvoiceDetailAsync(first);
        Assert.NotNull(detail);
        Assert.Single(detail!.Lines);
        Assert.True(detail.Header.TotalAmount > 0m);
        // Prices include VAT in this shop: the line total carries the tax, the invoice total is the
        // gross amount charged (subtotal minus the invoice discount).
        Assert.Equal(detail.Lines[0].LineTotalWithTax, detail.SubTotal + detail.Header.TaxAmount);
        Assert.Equal(detail.SubTotal - detail.Header.DiscountAmount, detail.Header.TotalAmount);
        Assert.Equal("محمود السيد", detail.Header.CashierName);
        Assert.True(detail.Lines[0].Profit >= 0m);

        Assert.Null(await reports.GetInvoiceDetailAsync(999_999));
    }

    [Fact]
    public async Task DailySalesSeries_IsAlignedWithTheRequestedRange()
    {
        await SellAsync();

        var from = DateTime.Today.AddDays(-6);
        var series = await CreateReportService().GetDailySalesAsync(new ReportFilter { FromDate = from, ToDate = DateTime.Today });

        Assert.Equal(7, series.Count);
        Assert.Equal(from, series[0].Date);
        Assert.Equal(DateTime.Today, series[^1].Date);
        Assert.True(series[^1].TotalSales > 0m);
        Assert.All(series.Take(6), point => Assert.Equal(0m, point.TotalSales));
    }

    [Fact]
    public void ReportFilter_NormalisesSwappedDates()
    {
        var filter = new ReportFilter { FromDate = DateTime.Today, ToDate = DateTime.Today.AddDays(-3) }.Normalize();

        Assert.True(filter.FromDate < filter.ToDate);
        Assert.Equal(4, filter.DayCount);   // today and the three days before it
    }

    /// <summary>Creates a customer on first use (the seed does not ship any).</summary>
    private async Task<Customer> EnsureCustomerAsync()
    {
        var existing = await _database.Context.Customers.FirstOrDefaultAsync();
        if (existing is not null)
        {
            return existing;
        }

        var customer = new Customer
        {
            Name = "أحمد عبد الله",
            PhoneNumber = "01001112223",
            IsActive = true
        };

        _database.Context.Customers.Add(customer);
        await _database.Context.SaveChangesAsync();
        return customer;
    }

    public void Dispose() => _database.Dispose();
}
