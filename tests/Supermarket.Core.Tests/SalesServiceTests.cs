using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>
/// Money-critical tests for the Phase 3 checkout engine and the shift lifecycle.
/// </summary>
public sealed class SalesServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database;
    private readonly FixedClock _clock = new(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));

    public SalesServiceTests() => _database = new SqliteTestDatabase();

    private StockService CreateStockService() => new(_database.Context, TestServices.Logger<StockService>());

    private SalesService CreateSalesService() => new(
        _database.Context,
        CreateStockService(),
        new NumberSequenceService(new TestContextFactory(_database), _clock),
        new StoreSettingsService(new TestContextFactory(_database)),
        _clock,
        TestServices.Logger<SalesService>());

    private ShiftService CreateShiftService() => new(
        _database.Context,
        new NumberSequenceService(new TestContextFactory(_database), _clock),
        new ReceiptFormatter(),
        new StoreSettingsService(new TestContextFactory(_database)),
        _clock,
        TestServices.Logger<ShiftService>());

    /// <summary>
    /// Builds a cart line. <paramref name="price"/> is the unit price the cashier sees; the tax rate is
    /// supplied explicitly because the sales service re-reads the rate (and the price) from the
    /// database, so tests that assert exact money figures use products whose configured rate matches.
    /// </summary>
    private static CartLineDto Line(int productId, string name, decimal price, decimal quantity, decimal vat = 14m, decimal discount = 0m) => new()
    {
        ProductId = productId,
        ProductName = name,
        Quantity = quantity,
        UnitPrice = price,
        UnitCost = price * 0.7m,
        DiscountAmount = discount,
        VatRate = vat
    };

    /// <summary>
    /// Picks a seeded piece product that can actually be sold (enough stock, not weighted) so the
    /// money assertions run against the real configured price instead of a made-up one.
    /// </summary>
    private async Task<Supermarket.Core.Entities.Product> SellableProductAsync(decimal minimumPrice = 0m) =>
        await _database.Context.Products.FirstAsync(p => p.SellingPrice >= minimumPrice
                                                         && p.StockQuantity >= 5m
                                                         && !p.IsWeighted
                                                         && p.IsActive);

    // ------------------------------------------------------------ Pricing math

    [Fact]
    public void CartLine_ComputesLineTotalTaxAndTotalWithTax()
    {
        var line = Line(1, "منتج", 27.00m, 3m);

        Assert.Equal(81.00m, line.LineTotal);
        Assert.Equal(11.34m, line.TaxAmount);       // 81 * 14%
        Assert.Equal(92.34m, line.LineTotalWithTax);
    }

    [Fact]
    public async Task PriceCart_AppliesPercentageDiscountAndExtractsVat()
    {
        var sales = CreateSalesService();

        // PriceCartAsync works on the supplied DTOs, so the VAT rate here is the one in the DTO.
        var lines = new[]
        {
            Line(1, "لبن", 100m, 1m, vat: 14m),
            Line(2, "جبن", 100m, 1m, vat: 14m)
        };

        var totals = await sales.PriceCartAsync(lines, invoiceDiscountPercentage: 10m);

        Assert.Equal(200m, totals.SubTotal);
        Assert.Equal(20m, totals.InvoiceDiscount);
        Assert.Equal(180m, totals.TotalAmount);      // prices include VAT
        Assert.Equal(25.2m, totals.TaxAmount);       // 28 VAT reduced by the 10% discount
        Assert.Equal(154.8m, totals.NetTotal);
        Assert.Equal(2, totals.LineCount);
        Assert.Equal(2m, totals.TotalQuantity);
    }

    [Fact]
    public async Task PriceCart_ConvertsAmountIntoPercentage()
    {
        var sales = CreateSalesService();
        var lines = new[] { Line(1, "منتج", 50m, 2m, vat: 14m) };

        var totals = await sales.PriceCartAsync(lines, invoiceDiscount: 25m);

        Assert.Equal(100m, totals.SubTotal);
        Assert.Equal(25m, totals.InvoiceDiscount);
        Assert.Equal(25m, totals.InvoiceDiscountPercentage);
        Assert.Equal(75m, totals.TotalAmount);
    }

    // ---------------------------------------------------------------- Checkout

    [Fact]
    public async Task CompleteSale_PersistsInvoiceLinesPaymentAndStockMovements()
    {
        var sales = CreateSalesService();
        var shift = await CreateShiftService().GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 500m });

        var product = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");
        var stockBefore = product.StockQuantity;

        var result = await sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            ShiftId = shift.Id,
            Lines = new[] { Line(product.Id, product.Name, product.SellingPrice, 2m, product.VatRate) },
            PaidAmount = 54m,
            PaymentMethod = PaymentMethod.Cash
        });

        // ---- invoice
        var invoice = await _database.Context.Invoices.Include(i => i.Items).Include(i => i.Payments)
            .SingleAsync(i => i.Id == result.InvoiceId);

        Assert.StartsWith("INV-", invoice.InvoiceNumber);
        Assert.Equal(InvoiceStatus.Completed, invoice.Status);
        Assert.Equal(54m, invoice.TotalAmount);
        Assert.Equal(54m, invoice.PaidAmount);
        Assert.Equal(0m, invoice.ChangeAmount);
        Assert.Single(invoice.Items);
        Assert.Single(invoice.Payments);
        Assert.Equal(PaymentMethod.Cash, invoice.Payments.First().Method);

        // ---- stock deducted through the ledger
        await _database.Context.Entry(product).ReloadAsync();
        Assert.Equal(stockBefore - 2m, product.StockQuantity);

        var movement = await _database.Context.StockMovements
            .Where(m => m.ProductId == product.Id && m.InvoiceId == invoice.Id)
            .SingleAsync();

        Assert.Equal(StockMovementType.Sale, movement.MovementType);
        Assert.Equal(-2m, movement.Quantity);
        Assert.Equal(invoice.InvoiceNumber, movement.ReferenceNumber);
        Assert.Equal(invoice.ShiftId, movement.ShiftId);
    }

    [Fact]
    public async Task CompleteSale_ComputesChangeDueAndReturnsReceiptData()
    {
        var sales = CreateSalesService();
        var shift = await CreateShiftService().GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 0m });

        // Product 8 (كوكا كولا) is seeded with a 14% VAT rate and a 26.00 EGP price.
        var product = await SellableProductAsync(minimumPrice: 20m);
        var price = product.SellingPrice;

        var result = await sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            ShiftId = shift.Id,
            Lines = new[] { Line(product.Id, product.Name, price, 1m) },
            PaidAmount = price + 50m,
            PaymentMethod = PaymentMethod.Cash
        });

        Assert.Equal(price, result.TotalAmount);
        Assert.Equal(price + 50m, result.PaidAmount);
        Assert.Equal(50m, result.ChangeAmount);   // المتبقي
        Assert.Equal("محمود السيد", result.CashierName);
        Assert.False(string.IsNullOrWhiteSpace(result.ShiftNumber));
        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task CompleteSale_UpdatesShiftCounters()
    {
        var sales = CreateSalesService();
        var shiftService = CreateShiftService();
        // The seeder already opened a shift for user 2 on POS-01 (500 EGP float), and GetOrOpenAsync
        // is idempotent, so this call returns the seeded shift instead of creating a second one.
        var shift = await shiftService.GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 300m });
        var openingCash = shift.OpeningCash;
        var product = await SellableProductAsync(minimumPrice: 20m);
        var price = product.SellingPrice;

        await sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            ShiftId = shift.Id,
            Lines = new[] { Line(product.Id, product.Name, price, 1m) },
            PaidAmount = price,
            PaymentMethod = PaymentMethod.Cash
        });

        await sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            ShiftId = shift.Id,
            Lines = new[] { Line(product.Id, product.Name, price, 1m) },
            PaidAmount = price,
            PaymentMethod = PaymentMethod.Card
        });

        var refreshed = await shiftService.GetOpenShiftAsync(2, "POS-01");

        Assert.NotNull(refreshed);
        Assert.Equal(2, refreshed!.InvoiceCount);
        Assert.Equal(price * 2, refreshed.TotalSales);
        Assert.Equal(price, refreshed.TotalCashSales);
        Assert.Equal(price, refreshed.TotalCardSales);
        Assert.Equal(openingCash + price, refreshed.ExpectedCash); // float + the single cash sale
    }

    [Fact]
    public async Task CompleteSale_MergesDuplicateScansAndRejectsInsufficientStock()
    {
        var sales = CreateSalesService();
        var product = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            Lines = new[]
            {
                Line(product.Id, product.Name, product.SellingPrice, product.StockQuantity),
                Line(product.Id, product.Name, product.SellingPrice, 1m) // merged -> stock + 1 > stock
            },
            PaidAmount = 10_000m,
            PaymentMethod = PaymentMethod.Cash
        }));
    }

    [Fact]
    public async Task CompleteSale_RejectsEmptyCartAndInactiveProductAndFractionalPieces()
    {
        var sales = CreateSalesService();
        var activeProduct = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            Lines = Array.Empty<CartLineDto>(),
            PaidAmount = 10m,
            PaymentMethod = PaymentMethod.Cash
        }));

        // Piece product sold with a fraction (IsWeighted == false).
        await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            Lines = new[] { Line(activeProduct.Id, activeProduct.Name, activeProduct.SellingPrice, 1.5m) },
            PaidAmount = 100m,
            PaymentMethod = PaymentMethod.Cash
        }));

        // Product deactivated after the cashier scanned it.
        activeProduct.IsActive = false;
        await _database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            Lines = new[] { Line(activeProduct.Id, activeProduct.Name, activeProduct.SellingPrice, 1m) },
            PaidAmount = 100m,
            PaymentMethod = PaymentMethod.Cash
        }));
    }

    [Fact]
    public async Task CompleteSale_RejectsUnderpaymentAndCreditWithoutCustomer()
    {
        var sales = CreateSalesService();
        var product = await SellableProductAsync(minimumPrice: 20m);
        var price = product.SellingPrice;

        var underpayment = await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            Lines = new[] { Line(product.Id, product.Name, price, 1m) },
            PaidAmount = price - 5m,
            PaymentMethod = PaymentMethod.Cash
        }));

        Assert.Contains("أقل من الإجمالي", underpayment.Message);

        var credit = await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            Lines = new[] { Line(product.Id, product.Name, price, 1m) },
            PaymentMethod = PaymentMethod.Credit
        }));

        Assert.Contains("عميل", credit.Message);
    }

    [Fact]
    public async Task CompleteSale_CreditSaleIncreasesCustomerBalance()
    {
        var sales = CreateSalesService();
        var product = await SellableProductAsync(minimumPrice: 20m);
        var price = product.SellingPrice;

        var customer = new Supermarket.Core.Entities.Customer
        {
            Name = "عميل آجل",
            PhoneNumber = "01111111111",
            Balance = 100m
        };
        _database.Context.Customers.Add(customer);
        await _database.Context.SaveChangesAsync();

        var result = await sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            Lines = new[] { Line(product.Id, product.Name, price, 1m) },
            PaymentMethod = PaymentMethod.Credit
        });

        await _database.Context.Entry(customer).ReloadAsync();

        Assert.Equal(100m + price, customer.Balance);
        Assert.Equal(price, result.RemainingAmount);
        Assert.Equal(0m, result.ChangeAmount);
    }

    [Fact]
    public async Task CompleteSale_MissingShiftIsRolledBack()
    {
        var sales = CreateSalesService();
        var product = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");
        var invoicesBefore = await _database.Context.Invoices.CountAsync();

        // The seeder only opens a shift for the cashier (user 2), so user 3 has no open shift:
        // the sale must be refused and nothing may be written.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 3,
            Lines = new[] { Line(product.Id, product.Name, product.SellingPrice, 1m) },
            PaidAmount = product.SellingPrice,
            PaymentMethod = PaymentMethod.Cash
        }));

        Assert.Contains("ورديّة", exception.Message);
        Assert.Equal(invoicesBefore, await _database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task GetInvoice_ReturnsReprintableSnapshot()
    {
        var sales = CreateSalesService();
        var product = await SellableProductAsync(minimumPrice: 20m);
        var price = product.SellingPrice;

        var created = await sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
            Lines = new[] { Line(product.Id, product.Name, price, 2m) },
            PaidAmount = price * 2 + 20m,
            PaymentMethod = PaymentMethod.Cash
        });

        var loaded = await sales.GetInvoiceAsync(created.InvoiceId);

        Assert.NotNull(loaded);
        Assert.Equal(created.InvoiceNumber, loaded!.InvoiceNumber);
        Assert.Equal(price * 2, loaded.TotalAmount);
        Assert.Equal(20m, loaded.ChangeAmount);
        Assert.Single(loaded.Lines);
        Assert.Equal(product.Name, loaded.Lines[0].ProductName);
    }

    // ----------------------------------------------------------------- Shifts

    [Fact]
    public async Task Shift_OpenIsIdempotentPerCashierAndTerminal()
    {
        var shifts = CreateShiftService();

        var first = await shifts.GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 200m, TerminalId = "POS-02" });
        var second = await shifts.GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 999m, TerminalId = "POS-02" });

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(200m, second.OpeningCash);
        Assert.Equal(200m, second.ExpectedCash);
        Assert.StartsWith("SH-", second.ShiftNumber);
    }

    [Fact]
    public async Task Shift_RejectsNegativeOpeningCash()
    {
        var shifts = CreateShiftService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            shifts.GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = -5m }));
    }

    public void Dispose() => _database.Dispose();
}
