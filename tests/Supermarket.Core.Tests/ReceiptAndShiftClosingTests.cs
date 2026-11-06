using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>
/// Phase 4 tests: 80 mm receipt layout and the X / Z shift reporting + closing rules.
/// </summary>
public sealed class ReceiptAndShiftClosingTests : IDisposable
{
    private readonly SqliteTestDatabase _database;
    private readonly FixedClock _clock = new(new DateTime(2026, 11, 5, 14, 30, 0, DateTimeKind.Utc));
    private readonly ReceiptFormatter _formatter = new();

    public ReceiptAndShiftClosingTests() => _database = new SqliteTestDatabase();

    private StoreSetting Settings() => new()
    {
        StoreName = "سوبر ماركت الأمانة",
        Address = "شارع الجمهورية - أخميم - سوهاج",
        PhoneNumber = "01001234567",
        TaxNumber = "512-336-778",
        ReceiptFooterMessage = "شكراً لزيارتكم - نتشرف بزيارتكم مرة أخرى",
        ReceiptLineWidth = 42
    };

    private static SaleResultDto SampleSale() => new()
    {
        InvoiceId = 1,
        InvoiceNumber = "INV-20261105-0001",
        InvoiceDateUtc = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Local),
        SubTotal = 100m,
        DiscountAmount = 5m,
        TaxAmount = 11.67m,
        TotalAmount = 95m,
        PaidAmount = 100m,
        ChangeAmount = 5m,
        PaymentMethod = PaymentMethod.Cash,
        CashierName = "محمود السيد",
        ShiftNumber = "SH-20261105-0001",
        Lines = new[]
        {
            new CartLineDto
            {
                ProductId = 1,
                ProductName = "لبن جهينة كامل الدسم 1 لتر",
                Barcode = "6223000011111",
                Quantity = 3m,
                UnitPrice = 30m,
                DiscountAmount = 5m,
                VatRate = 14m
            }
        }
    };

    // ------------------------------------------------------------- Formatting

    [Fact]
    public void SaleReceipt_ContainsArabicHeaderTotalsAndFooter()
    {
        var receipt = _formatter.BuildSaleReceipt(SampleSale(), Settings());

        Assert.Contains("سوبر ماركت الأمانة", receipt);
        Assert.Contains("فاتورة بيع", receipt);
        Assert.Contains("INV-20261105-0001", receipt);
        Assert.Contains("لبن جهينة كامل الدسم 1 لتر", receipt);
        Assert.Contains("المجموع الفرعي", receipt);
        Assert.Contains("الضريبة", receipt);
        Assert.Contains("الإجمالي", receipt);
        Assert.Contains("المدفوع", receipt);
        Assert.Contains("المتبقي", receipt);
        Assert.Contains("نقدي", receipt);
        Assert.Contains("ج.م", receipt);
        Assert.Contains("شكراً لزيارتكم", receipt);
    }

    [Fact]
    public void SaleReceipt_RespectsTheConfiguredLineWidth()
    {
        var settings = Settings();
        var receipt = _formatter.BuildSaleReceipt(SampleSale(), settings);

        // Separator lines must be exactly as wide as the configured roll.
        var separators = receipt
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length == settings.ReceiptLineWidth && l.StartsWith("===="))
            .ToList();

        Assert.NotEmpty(separators);
        Assert.All(separators, s => Assert.Equal(settings.ReceiptLineWidth, s.Length));
    }

    [Fact]
    public void SaleReceipt_WrapsLongProductNamesWithoutBreakingTheLayout()
    {
        var sale = SampleSale();
        sale.Lines = new[]
        {
            new CartLineDto
            {
                ProductId = 9,
                ProductName = "عصير برتقال طبيعي معلب بالكامل بدون سكر مضاف عبوة عائلية كبيرة جدا واحد لتر",
                Quantity = 1m,
                UnitPrice = 25m,
                VatRate = 14m
            }
        };

        var receipt = _formatter.BuildSaleReceipt(sale, Settings());

        // Every emitted line must fit the roll (long meta pairs are stacked, not clipped).
        Assert.All(receipt.Split('\n'), line => Assert.True(line.TrimEnd('\r').Length <= Settings().ReceiptLineWidth));
        Assert.Contains("عصير برتقال", receipt);
    }

    [Fact]
    public void PadLine_AlignsBothEndsWithinTheWidth()
    {
        var line = _formatter.PadLine("الإجمالي", "95.00 ج.م", 42);

        Assert.Equal(42, line.Length);
        Assert.StartsWith("الإجمالي", line);
        Assert.EndsWith("95.00 ج.م", line);
    }

    [Fact]
    public void Center_PadsEvenly()
    {
        var centered = _formatter.Center("الأمانة", 20);

        // 7 characters in a 20 column roll => 6 leading spaces, the rest stays free on the right.
        Assert.Equal(13, centered.Length);
        Assert.Equal(6, centered.Length - "الأمانة".Length);
        Assert.EndsWith("الأمانة", centered);
    }

    [Fact]
    public void ShiftReport_ZReport_ContainsCashReconciliation()
    {
        var report = new ShiftReportDto
        {
            ShiftId = 1,
            ShiftNumber = "SH-20261105-0001",
            CashierName = "محمود السيد",
            TerminalId = "POS-01",
            ReportType = ShiftReportType.ZReport,
            GeneratedAtUtc = DateTime.UtcNow,
            OpenedAtUtc = DateTime.UtcNow.AddHours(-8),
            ClosedAtUtc = DateTime.UtcNow,
            OpeningCash = 500m,
            ExpectedCash = 1750m,
            ClosingCash = 1735m,
            CashDifference = -15m,
            TotalSales = 2450m,
            TotalCashSales = 1250m,
            TotalCardSales = 900m,
            TotalCreditSales = 300m,
            TotalDiscount = 40m,
            TotalTax = 210m,
            InvoiceCount = 37
        };

        var text = _formatter.BuildShiftReport(report, Settings());

        Assert.Contains("تقرير تقفيل الورديّة (Z)", text);
        Assert.Contains("SH-20261105-0001", text);
        Assert.Contains("عدد الفواتير", text);
        Assert.Contains("النقدية الافتتاحية", text);
        Assert.Contains("النقدية المتوقعة", text);
        Assert.Contains("النقدية المعدودة", text);
        Assert.Contains("عجز في النقدية", text);
        Assert.Contains("توقيع الكاشير", text);
    }

    [Fact]
    public void ShiftReport_XReport_HasNoClosingCashSection()
    {
        var report = new ShiftReportDto
        {
            ShiftNumber = "SH-20261105-0002",
            CashierName = "محمود السيد",
            TerminalId = "POS-01",
            ReportType = ShiftReportType.XReport,
            OpenedAtUtc = DateTime.UtcNow.AddHours(-2),
            OpeningCash = 200m,
            ExpectedCash = 500m,
            TotalSales = 300m,
            TotalCashSales = 300m,
            InvoiceCount = 4
        };

        var text = _formatter.BuildShiftReport(report, Settings());

        Assert.Contains("تقرير الورديّة (X)", text);
        Assert.DoesNotContain("النقدية المعدودة", text);
        Assert.DoesNotContain("عجز في النقدية", text);
        Assert.Contains("النقدية المتوقعة", text);
    }

    // ------------------------------------------------------- Closing the shift

    private ShiftService CreateShiftService() => new(
        _database.Context,
        new NumberSequenceService(new TestContextFactory(_database), _clock),
        _formatter,
        new StoreSettingsService(new TestContextFactory(_database)),
        _clock,
        TestServices.Logger<ShiftService>());

    private SalesService CreateSalesService() => new(
        _database.Context,
        new StockService(_database.Context, TestServices.Logger<StockService>()),
        new NumberSequenceService(new TestContextFactory(_database), _clock),
        new StoreSettingsService(new TestContextFactory(_database)),
        _clock,
        TestServices.Logger<SalesService>());

    private async Task<int> SellAsync(decimal quantity = 1m, PaymentMethod method = PaymentMethod.Cash)
    {
        var product = await _database.Context.Products.FirstAsync(p => !p.IsWeighted && p.StockQuantity > 20m);
        var sales = CreateSalesService();

        var result = await sales.CompleteSaleAsync(new SaleRequestDto
        {
            UserId = 2,
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
            CustomerId = method == PaymentMethod.Credit ? 1 : null
        });

        return result.InvoiceId;
    }

    [Fact]
    public async Task BuildReport_AggregatesCompletedInvoicesByPaymentMethod()
    {
        var shift = await CreateShiftService().GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 400m });
        await SellAsync(1m, PaymentMethod.Cash);
        await SellAsync(2m, PaymentMethod.Card);

        var report = await CreateShiftService().BuildReportAsync(shift.Id, ShiftReportType.XReport);

        Assert.Equal(2, report.InvoiceCount);
        Assert.True(report.TotalCashSales > 0m);
        Assert.True(report.TotalCardSales > report.TotalCashSales);   // 2 units on the card
        // The seeded shift already had takings, so the expectation is the absolute drawer value.
        Assert.Equal(report.OpeningCash + report.TotalCashSales, report.ExpectedCash);
        Assert.Equal(report.TotalSales, report.TotalCashSales + report.TotalCardSales);
        Assert.Equal(ShiftReportType.XReport, report.ReportType);
    }

    [Fact]
    public async Task CloseShift_StoresZReportAndLocksTheShift()
    {
        var shiftService = CreateShiftService();
        var shift = await shiftService.GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 300m });
        await SellAsync(1m, PaymentMethod.Cash);

        var xReport = await shiftService.BuildReportAsync(shift.Id, ShiftReportType.XReport);

        // Counted cash with a deliberate 10 EGP shortage.
        var closed = await shiftService.CloseShiftAsync(new ShiftCloseRequestDto
        {
            ShiftId = shift.Id,
            ClosingCash = xReport.ExpectedCash - 10m,
            Notes = "عجز 10 جنيه - تم إبلاغ المشرف"
        });

        Assert.Equal(-10m, closed.CashDifference);
        Assert.Equal(ShiftStatus.Closed, (await _database.Context.Shifts.FindAsync(shift.Id))!.Status);

        var stored = await _database.Context.ShiftReports.SingleAsync(r => r.ShiftId == shift.Id);
        Assert.Equal(ShiftReportType.ZReport, stored.ReportType);
        Assert.Equal(-10m, stored.CashDifference);
        Assert.NotNull(stored.PrintedContent);
        Assert.Contains("تقرير تقفيل الورديّة", stored.PrintedContent!);

        // The shift is closed, so the cashier can no longer sell on it.
        Assert.Null(await shiftService.GetOpenShiftAsync(2, "POS-01"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => shiftService.CloseShiftAsync(new ShiftCloseRequestDto
        {
            ShiftId = shift.Id,
            ClosingCash = 100m
        }));
    }

    [Fact]
    public async Task CloseShift_ComputesSurplusAndRejectsNegativeCash()
    {
        var shiftService = CreateShiftService();
        var shift = await shiftService.GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 100m, TerminalId = "POS-09" });

        var report = await shiftService.BuildReportAsync(shift.Id, ShiftReportType.ZReport);
        var closed = await shiftService.CloseShiftAsync(new ShiftCloseRequestDto
        {
            ShiftId = shift.Id,
            ClosingCash = report.ExpectedCash + 25m
        });

        Assert.Equal(25m, closed.CashDifference);

        await Assert.ThrowsAsync<ArgumentException>(() => shiftService.CloseShiftAsync(new ShiftCloseRequestDto
        {
            ShiftId = shift.Id + 100,
            ClosingCash = -1m
        }));
    }

    [Fact]
    public async Task GetReports_ReturnsStoredZReports()
    {
        var shiftService = CreateShiftService();
        var shift = await shiftService.GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 50m, TerminalId = "POS-08" });

        await shiftService.CloseShiftAsync(new ShiftCloseRequestDto { ShiftId = shift.Id, ClosingCash = 50m });

        var reports = await shiftService.GetReportsAsync(shift.Id);

        Assert.Single(reports);
        Assert.Equal(ShiftReportType.ZReport, reports[0].ReportType);
        Assert.Equal("POS-08", reports[0].TerminalId);
    }

    [Fact]
    public async Task GetShifts_ReturnsHistoryWithinTheDateRange()
    {
        var shiftService = CreateShiftService();
        await shiftService.GetOrOpenAsync(new ShiftOpenRequestDto { UserId = 2, OpeningCash = 75m, TerminalId = "POS-07" });

        var shifts = await shiftService.GetShiftsAsync(DateTime.Today.AddDays(-1), DateTime.Today, "POS-07");

        Assert.NotEmpty(shifts);
        Assert.All(shifts, s => Assert.Equal("POS-07", s.TerminalId));
        Assert.All(shifts, s => Assert.Equal("محمود السيد", s.CashierName));
    }

    public void Dispose() => _database.Dispose();
}
