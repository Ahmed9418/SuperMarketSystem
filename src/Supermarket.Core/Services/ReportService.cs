using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Read-only reporting for the admin back office (Phase 5).
///
/// <para>
/// Everything is aggregated in SQL — no invoice is ever loaded to be summed in memory, which keeps the
/// screens responsive on a year of data. The same <see cref="ReportFilter"/> drives the KPI tiles, the
/// four charts and the grids, so all of them always describe the same period.
/// </para>
///
/// <para>
/// The period is expressed in local days (the shop's day) and converted to UTC before querying,
/// because every timestamp in the database is stored as UTC.
/// </para>
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly SupermarketDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ReportService(SupermarketDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(ReportFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Normalize();

        var (fromUtc, toUtcExclusive) = ToUtcWindow(filter.FromDate, filter.ToDate);

        // Completed invoices only: cancelled / draft rows never enter a sales figure.
        var invoices = _db.Invoices
            .AsNoTracking()
            .Where(i => i.InvoiceDateUtc >= fromUtc && i.InvoiceDateUtc < toUtcExclusive);

        var completed = invoices.Where(i => i.Status == InvoiceStatus.Completed);

        if (filter.UserId.HasValue)
        {
            completed = completed.Where(i => i.UserId == filter.UserId.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            completed = completed.Where(i => i.PaymentMethod == filter.PaymentMethod.Value);
        }

        var result = new SalesAnalyticsDto
        {
            Filter = filter,
            GeneratedAtUtc = _clock.UtcNow
        };

        // ------------------------------------------------------------------ KPI tiles
        var totals = await completed
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Gross = g.Sum(i => i.SubTotal),
                Discount = g.Sum(i => i.DiscountAmount),
                Tax = g.Sum(i => i.TaxAmount),
                Net = g.Sum(i => i.TotalAmount),
                Credit = g.Where(i => i.PaymentMethod == PaymentMethod.Credit).Sum(i => i.TotalAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (totals is not null)
        {
            result.InvoiceCount = totals.Count;
            result.GrossSales = totals.Gross;
            result.TotalDiscount = totals.Discount;
            result.TotalTax = totals.Tax;
            result.NetSales = totals.Net;
            result.CreditSales = totals.Credit;
        }

        result.CancelledInvoiceCount = await invoices.CountAsync(i => i.Status == InvoiceStatus.Cancelled, cancellationToken);
        result.RefundedInvoiceCount = await invoices.CountAsync(i => i.Status == InvoiceStatus.Refunded, cancellationToken);

        var lineTotals = await _db.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice!.InvoiceDateUtc >= fromUtc
                         && ii.Invoice.InvoiceDateUtc < toUtcExclusive
                         && ii.Invoice.Status == InvoiceStatus.Completed
                         && (!filter.UserId.HasValue || ii.Invoice.UserId == filter.UserId.Value)
                         && (!filter.PaymentMethod.HasValue || ii.Invoice.PaymentMethod == filter.PaymentMethod.Value))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Lines = g.Count(),
                Quantity = g.Sum(ii => ii.Quantity),
                Profit = g.Sum(ii => (ii.UnitPrice - ii.UnitCost) * ii.Quantity - ii.DiscountAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (lineTotals is not null)
        {
            result.ItemsSold = lineTotals.Lines;
            result.TotalQuantity = lineTotals.Quantity;
            result.TotalProfit = lineTotals.Profit;
        }

        // ------------------------------------------------------------------ Closed shifts
        var closedShifts = _db.Shifts
            .AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Closed
                        && s.OpenedAtUtc >= fromUtc
                        && s.OpenedAtUtc < toUtcExclusive);

        if (filter.UserId.HasValue)
        {
            closedShifts = closedShifts.Where(s => s.UserId == filter.UserId.Value);
        }

        result.ClosedShifts = await closedShifts.CountAsync(cancellationToken);
        result.CashDifference = await closedShifts.SumAsync(s => (decimal?)s.CashDifference, cancellationToken) ?? 0m;

        // ------------------------------------------------------------------ Daily series
        result.DailySales = await BuildDailySeriesAsync(completed, filter, cancellationToken);

        // ------------------------------------------------------------------ Payment split
        result.PaymentBreakdown = await BuildPaymentBreakdownAsync(completed, cancellationToken);

        // ------------------------------------------------------------------ Categories
        result.CategorySales = await BuildCategorySalesAsync(fromUtc, toUtcExclusive, filter, cancellationToken);

        // ------------------------------------------------------------------ Top products
        result.TopProducts = await BuildTopProductsAsync(fromUtc, toUtcExclusive, filter, 10, cancellationToken);

        // ------------------------------------------------------------------ Cashiers
        result.Cashiers = await BuildCashierPerformanceAsync(fromUtc, toUtcExclusive, filter, cancellationToken);

        // ------------------------------------------------------------------ Stock by category
        result.CategoryStock = await BuildCategoryStockAsync(cancellationToken);

        // ------------------------------------------------------------------ Previous period
        var dayCount = filter.DayCount;
        var previousFrom = fromUtc.AddDays(-dayCount);
        var previousInvoices = _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Completed
                        && i.InvoiceDateUtc >= previousFrom
                        && i.InvoiceDateUtc < fromUtc);

        if (filter.UserId.HasValue)
        {
            previousInvoices = previousInvoices.Where(i => i.UserId == filter.UserId.Value);
        }

        result.PreviousPeriodSales = await previousInvoices.SumAsync(i => (decimal?)i.TotalAmount, cancellationToken) ?? 0m;

        return result;
    }

    /// <inheritdoc />
    public async Task<PagedResult<InvoiceListDto>> GetInvoicesAsync(InvoiceFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.FromDate.Date > filter.ToDate.Date)
        {
            (filter.FromDate, filter.ToDate) = (filter.ToDate, filter.FromDate);
        }

        filter.PageNumber = Math.Max(1, filter.PageNumber);
        filter.PageSize = Math.Clamp(filter.PageSize, 1, 200);

        var (fromUtc, toUtcExclusive) = ToUtcWindow(filter.FromDate, filter.ToDate);

        var query = _db.Invoices
            .AsNoTracking()
            .Where(i => i.InvoiceDateUtc >= fromUtc && i.InvoiceDateUtc < toUtcExclusive);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(term)
                                     || (i.Customer != null && i.Customer.Name.Contains(term)));
        }

        if (filter.UserId.HasValue)
        {
            query = query.Where(i => i.UserId == filter.UserId.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            query = query.Where(i => i.PaymentMethod == filter.PaymentMethod.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(i => i.Status == filter.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(i => i.InvoiceDateUtc)
            .ThenByDescending(i => i.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(i => new InvoiceListDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDateUtc = i.InvoiceDateUtc,
                CashierName = i.User != null ? i.User.FullName : string.Empty,
                CustomerName = i.Customer != null ? i.Customer.Name : null,
                ShiftNumber = i.Shift != null ? i.Shift.ShiftNumber : string.Empty,
                ItemCount = i.Items.Count,
                TotalAmount = i.TotalAmount,
                TaxAmount = i.TaxAmount,
                DiscountAmount = i.DiscountAmount,
                PaidAmount = i.PaidAmount,
                RemainingAmount = i.RemainingAmount,
                PaymentMethod = i.PaymentMethod,
                Status = i.Status
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceListDto>(rows, totalCount, filter.PageNumber, filter.PageSize);
    }

    /// <inheritdoc />
    public async Task<InvoiceDetailDto?> GetInvoiceDetailAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var header = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new InvoiceDetailDto
            {
                Header = new InvoiceListDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDateUtc = i.InvoiceDateUtc,
                    CashierName = i.User != null ? i.User.FullName : string.Empty,
                    CustomerName = i.Customer != null ? i.Customer.Name : null,
                    ShiftNumber = i.Shift != null ? i.Shift.ShiftNumber : string.Empty,
                    ItemCount = i.Items.Count,
                    TotalAmount = i.TotalAmount,
                    TaxAmount = i.TaxAmount,
                    DiscountAmount = i.DiscountAmount,
                    PaidAmount = i.PaidAmount,
                    RemainingAmount = i.RemainingAmount,
                    PaymentMethod = i.PaymentMethod,
                    Status = i.Status
                },
                SubTotal = i.SubTotal,
                ChangeAmount = i.ChangeAmount,
                Notes = i.Notes,
                Lines = i.Items
                    .OrderBy(ii => ii.Id)
                    .Select(ii => new InvoiceLineDto
                    {
                        ProductId = ii.ProductId,
                        ProductName = ii.ProductName,
                        Barcode = ii.Barcode,
                        Quantity = ii.Quantity,
                        UnitPrice = ii.UnitPrice,
                        DiscountAmount = ii.DiscountAmount,
                        TaxAmount = ii.TaxAmount,
                        LineTotal = ii.LineTotal,
                        LineTotalWithTax = ii.LineTotalWithTax,
                        Profit = (ii.UnitPrice - ii.UnitCost) * ii.Quantity - ii.DiscountAmount
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return header;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(
        ReportFilter filter,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        filter.Normalize();
        var (fromUtc, toUtcExclusive) = ToUtcWindow(filter.FromDate, filter.ToDate);
        return await BuildTopProductsAsync(fromUtc, toUtcExclusive, filter, Math.Clamp(take, 1, 100), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailySalesDto>> GetDailySalesAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        filter.Normalize();
        var (fromUtc, toUtcExclusive) = ToUtcWindow(filter.FromDate, filter.ToDate);

        var invoices = _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Completed
                        && i.InvoiceDateUtc >= fromUtc
                        && i.InvoiceDateUtc < toUtcExclusive);

        if (filter.UserId.HasValue)
        {
            invoices = invoices.Where(i => i.UserId == filter.UserId.Value);
        }

        var series = await BuildDailySeriesAsync(invoices, filter, cancellationToken);

        return series.Select(p => new DailySalesDto
        {
            Date = p.Date,
            TotalSales = p.Sales,
            TotalProfit = p.Profit,
            InvoiceCount = p.InvoiceCount
        }).ToList();
    }

    // ---------------------------------------------------------------------------------------
    // Building blocks
    // ---------------------------------------------------------------------------------------

    private static async Task<IReadOnlyList<SalesTrendPointDto>> BuildDailySeriesAsync(
        IQueryable<Invoice> completed,
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        // Grouped by the local calendar day: the stored value is UTC, so rows are fetched and bucketed
        // by local day in memory — exact across midnight boundaries and DST changes.
        var raw = await completed
            .Select(i => new
            {
                i.InvoiceDateUtc,
                i.TotalAmount,
                Profit = i.Items.Sum(ii => (ii.UnitPrice - ii.UnitCost) * ii.Quantity - ii.DiscountAmount)
            })
            .ToListAsync(cancellationToken);

        var buckets = raw
            .GroupBy(r => DateTime.SpecifyKind(r.InvoiceDateUtc, DateTimeKind.Utc).ToLocalTime().Date)
            .ToDictionary(g => g.Key, g => new
            {
                Sales = g.Sum(x => x.TotalAmount),
                Profit = g.Sum(x => x.Profit),
                Count = g.Count()
            });

        var series = new List<SalesTrendPointDto>(filter.DayCount);

        // Empty days are emitted as zeros so the chart keeps a continuous time axis.
        for (var day = filter.FromDate.Date; day <= filter.ToDate.Date; day = day.AddDays(1))
        {
            if (buckets.TryGetValue(day, out var bucket))
            {
                series.Add(new SalesTrendPointDto
                {
                    Date = day,
                    Sales = bucket.Sales,
                    Profit = bucket.Profit,
                    InvoiceCount = bucket.Count
                });
            }
            else
            {
                series.Add(new SalesTrendPointDto { Date = day });
            }
        }

        return series;
    }

    private static async Task<IReadOnlyList<PaymentBreakdownDto>> BuildPaymentBreakdownAsync(
        IQueryable<Invoice> completed,
        CancellationToken cancellationToken)
    {
        var raw = await completed
            .GroupBy(i => i.PaymentMethod)
            .Select(g => new
            {
                Method = g.Key,
                Amount = g.Sum(i => i.TotalAmount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var total = raw.Sum(r => r.Amount);

        // Always the four methods in enum order, so the pie legend never jumps around.
        return Enum.GetValues<PaymentMethod>()
            .Select(method =>
            {
                var row = raw.FirstOrDefault(r => r.Method == method);
                var amount = row?.Amount ?? 0m;

                return new PaymentBreakdownDto
                {
                    Method = method,
                    Amount = amount,
                    InvoiceCount = row?.Count ?? 0,
                    Share = total == 0m ? 0m : Math.Round(amount / total * 100m, 1)
                };
            })
            .Where(p => p.InvoiceCount > 0 || p.Amount > 0m)
            .ToList();
    }

    private async Task<IReadOnlyList<CategorySalesDto>> BuildCategorySalesAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        var lineQuery = BuildLineQuery(fromUtc, toUtcExclusive, filter);

        return await lineQuery
            .GroupBy(ii => new { ii.Product!.CategoryId, CategoryName = ii.Product!.Category!.Name })
            .Select(g => new CategorySalesDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Sales = g.Sum(ii => ii.LineTotalWithTax),
                Profit = g.Sum(ii => (ii.UnitPrice - ii.UnitCost) * ii.Quantity - ii.DiscountAmount),
                Quantity = g.Sum(ii => ii.Quantity),
                LineCount = g.Count()
            })
            .OrderByDescending(c => c.Sales)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TopProductDto>> BuildTopProductsAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        ReportFilter filter,
        int take,
        CancellationToken cancellationToken)
    {
        var lineQuery = BuildLineQuery(fromUtc, toUtcExclusive, filter);

        return await lineQuery
            .GroupBy(ii => new { ii.ProductId, ii.ProductName })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                QuantitySold = g.Sum(ii => ii.Quantity),
                Revenue = g.Sum(ii => ii.LineTotalWithTax),
                Profit = g.Sum(ii => (ii.UnitPrice - ii.UnitCost) * ii.Quantity - ii.DiscountAmount)
            })
            .OrderByDescending(p => p.Revenue)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Invoice lines of completed invoices inside the UTC window, honouring the filter.</summary>
    private IQueryable<InvoiceItem> BuildLineQuery(DateTime fromUtc, DateTime toUtcExclusive, ReportFilter filter)
    {
        var query = _db.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice!.InvoiceDateUtc >= fromUtc
                         && ii.Invoice.InvoiceDateUtc < toUtcExclusive
                         && ii.Invoice.Status == InvoiceStatus.Completed);

        if (filter.UserId.HasValue)
        {
            query = query.Where(ii => ii.Invoice!.UserId == filter.UserId.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            query = query.Where(ii => ii.Invoice!.PaymentMethod == filter.PaymentMethod.Value);
        }

        return query;
    }

    private async Task<IReadOnlyList<CashierPerformanceDto>> BuildCashierPerformanceAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        var invoiceQuery = _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Completed
                        && i.InvoiceDateUtc >= fromUtc
                        && i.InvoiceDateUtc < toUtcExclusive);

        if (filter.UserId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(i => i.UserId == filter.UserId.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(i => i.PaymentMethod == filter.PaymentMethod.Value);
        }

        var sales = await invoiceQuery
            .GroupBy(i => new { i.UserId, Name = i.User!.FullName, Role = i.User!.Role })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Name,
                g.Key.Role,
                Count = g.Count(),
                Sales = g.Sum(i => i.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        var profits = await _db.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice!.InvoiceDateUtc >= fromUtc
                         && ii.Invoice.InvoiceDateUtc < toUtcExclusive
                         && ii.Invoice.Status == InvoiceStatus.Completed
                         && (!filter.UserId.HasValue || ii.Invoice.UserId == filter.UserId.Value)
                         && (!filter.PaymentMethod.HasValue || ii.Invoice.PaymentMethod == filter.PaymentMethod.Value))
            .GroupBy(ii => ii.Invoice!.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Profit = g.Sum(ii => (ii.UnitPrice - ii.UnitCost) * ii.Quantity - ii.DiscountAmount)
            })
            .ToListAsync(cancellationToken);

        var shifts = await _db.Shifts
            .AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Closed
                        && s.OpenedAtUtc >= fromUtc
                        && s.OpenedAtUtc < toUtcExclusive
                        && (!filter.UserId.HasValue || s.UserId == filter.UserId.Value))
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Difference = g.Sum(s => s.CashDifference)
            })
            .ToListAsync(cancellationToken);

        return sales
            .Select(s =>
            {
                var profit = profits.FirstOrDefault(p => p.UserId == s.UserId)?.Profit ?? 0m;
                var shift = shifts.FirstOrDefault(x => x.UserId == s.UserId);

                return new CashierPerformanceDto
                {
                    UserId = s.UserId,
                    CashierName = s.Name,
                    RoleName = s.Role.ToArabic(),
                    InvoiceCount = s.Count,
                    Sales = s.Sales,
                    Profit = profit,
                    AverageInvoice = s.Count == 0 ? 0m : Math.Round(s.Sales / s.Count, 2),
                    ShiftCount = shift?.Count ?? 0,
                    CashDifference = shift?.Difference ?? 0m
                };
            })
            .OrderByDescending(c => c.Sales)
            .ToList();
    }

    private async Task<IReadOnlyList<CategoryStockDto>> BuildCategoryStockAsync(CancellationToken cancellationToken) =>
        await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(p => new { p.CategoryId, Name = p.Category!.Name })
            .Select(g => new CategoryStockDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.Name,
                ProductCount = g.Count(),
                LowStockCount = g.Count(p => p.StockQuantity <= p.MinimumStockLevel),
                Quantity = g.Sum(p => p.StockQuantity),
                StockValue = g.Sum(p => p.StockQuantity * p.CostPrice)
            })
            .OrderByDescending(c => c.StockValue)
            .ToListAsync(cancellationToken);

    /// <summary>Converts a local, whole-day range into the exclusive UTC window used by the queries.</summary>
    private static (DateTime FromUtc, DateTime ToUtcExclusive) ToUtcWindow(DateTime fromDate, DateTime toDate)
    {
        var from = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Local).ToUniversalTime();
        var to = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        return (from, to);
    }
}
