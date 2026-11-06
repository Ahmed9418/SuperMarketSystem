using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Shift lifecycle of the POS: opening, live figures (X report), closing (Z report) and history.
/// The report figures are always rebuilt from the invoices, never from the cached counters on the
/// shift row, so a manual data fix can never distort the drawer reconciliation.
/// </summary>
public sealed class ShiftService : IShiftService
{
    private readonly SupermarketDbContext _db;
    private readonly INumberSequenceService _numberSequence;
    private readonly IReceiptFormatter _receiptFormatter;
    private readonly IStoreSettingsService _storeSettings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ShiftService> _logger;

    public ShiftService(
        SupermarketDbContext db,
        INumberSequenceService numberSequence,
        IReceiptFormatter receiptFormatter,
        IStoreSettingsService storeSettings,
        IDateTimeProvider clock,
        ILogger<ShiftService> logger)
    {
        _db = db;
        _numberSequence = numberSequence;
        _receiptFormatter = receiptFormatter;
        _storeSettings = storeSettings;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ShiftDto?> GetOpenShiftAsync(int userId, string terminalId, CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == ShiftStatus.Open && s.TerminalId == terminalId)
            .OrderByDescending(s => s.OpenedAtUtc)
            .Select(s => new ShiftDto
            {
                Id = s.Id,
                ShiftNumber = s.ShiftNumber,
                UserId = s.UserId,
                CashierName = s.User != null ? s.User.FullName : string.Empty,
                TerminalId = s.TerminalId,
                OpenedAtUtc = s.OpenedAtUtc,
                ClosedAtUtc = s.ClosedAtUtc,
                Status = s.Status,
                OpeningCash = s.OpeningCash,
                ClosingCash = s.ClosingCash,
                ExpectedCash = s.ExpectedCash,
                CashDifference = s.CashDifference,
                TotalSales = s.TotalSales,
                TotalCashSales = s.TotalCashSales,
                TotalCardSales = s.TotalCardSales,
                TotalWalletSales = s.TotalWalletSales,
                TotalCreditSales = s.TotalCreditSales,
                TotalDiscount = s.TotalDiscount,
                TotalTax = s.TotalTax,
                InvoiceCount = s.InvoiceCount
            })
            .FirstOrDefaultAsync(cancellationToken);

        return shift;
    }

    /// <inheritdoc />
    public async Task<ShiftDto> GetOrOpenAsync(ShiftOpenRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.UserId <= 0)
        {
            throw new ArgumentException("يجب تسجيل دخول الكاشير أولاً");
        }

        if (request.OpeningCash < 0m)
        {
            throw new ArgumentException("النقدية الافتتاحية لا يمكن أن تكون سالبة");
        }

        var terminalId = string.IsNullOrWhiteSpace(request.TerminalId) ? "POS-01" : request.TerminalId.Trim();

        var existing = await GetOpenShiftAsync(request.UserId, terminalId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var cashier = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("لم يتم العثور على بيانات الكاشير");

        var shiftNumber = await _numberSequence.NextShiftNumberAsync(cancellationToken);

        var shift = new Shift
        {
            ShiftNumber = shiftNumber,
            UserId = request.UserId,
            TerminalId = terminalId,
            OpenedAtUtc = _clock.UtcNow,
            Status = ShiftStatus.Open,
            OpeningCash = request.OpeningCash,
            ExpectedCash = request.OpeningCash,
            Notes = request.Notes,
            CreatedBy = cashier.UserName
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Shift {ShiftNumber} opened by {Cashier} on {Terminal} with {Cash} EGP",
            shiftNumber, cashier.UserName, terminalId, request.OpeningCash);

        return new ShiftDto
        {
            Id = shift.Id,
            ShiftNumber = shift.ShiftNumber,
            UserId = shift.UserId,
            CashierName = cashier.FullName,
            TerminalId = shift.TerminalId,
            OpenedAtUtc = shift.OpenedAtUtc,
            Status = shift.Status,
            OpeningCash = shift.OpeningCash,
            ExpectedCash = shift.ExpectedCash
        };
    }

    /// <inheritdoc />
    public async Task<ShiftReportDto> BuildReportAsync(int shiftId, ShiftReportType reportType, CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new InvalidOperationException($"Shift {shiftId} was not found.");

        return await BuildReportCoreAsync(shift, reportType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ShiftReportDto> CloseShiftAsync(ShiftCloseRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.ClosingCash < 0m)
        {
            throw new ArgumentException("النقدية المعدودة لا يمكن أن تكون سالبة");
        }

        var shift = await _db.Shifts
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken)
            ?? throw new InvalidOperationException($"Shift {request.ShiftId} was not found.");

        if (shift.Status == ShiftStatus.Closed)
        {
            throw new InvalidOperationException($"الورديّة {shift.ShiftNumber} مغلقة بالفعل");
        }

        // Figures are recomputed from the invoices of the shift (single source of truth).
        var report = await BuildReportCoreAsync(shift, ShiftReportType.ZReport, cancellationToken);

        report.ClosingCash = Math.Round(request.ClosingCash, 2);
        report.CashDifference = Math.Round(report.ClosingCash.Value - report.ExpectedCash, 2);
        report.ClosedAtUtc = _clock.UtcNow;
        report.GeneratedAtUtc = _clock.UtcNow;

        shift.Status = ShiftStatus.Closed;
        shift.ClosedAtUtc = report.ClosedAtUtc;
        shift.ClosingCash = report.ClosingCash;
        shift.ExpectedCash = report.ExpectedCash;
        shift.CashDifference = report.CashDifference;
        shift.ClosedBy = string.IsNullOrWhiteSpace(request.ClosedBy) ? shift.User?.FullName : request.ClosedBy;
        shift.Notes = string.IsNullOrWhiteSpace(request.Notes) ? shift.Notes : request.Notes;

        // Keep the cached counters aligned with the recomputed figures.
        shift.TotalSales = report.TotalSales;
        shift.TotalCashSales = report.TotalCashSales;
        shift.TotalCardSales = report.TotalCardSales;
        shift.TotalWalletSales = report.TotalWalletSales;
        shift.TotalCreditSales = report.TotalCreditSales;
        shift.TotalDiscount = report.TotalDiscount;
        shift.TotalTax = report.TotalTax;
        shift.TotalRefunds = report.TotalRefunds;
        shift.InvoiceCount = report.InvoiceCount;

        var settings = await _storeSettings.GetAsync(cancellationToken);
        var printedContent = _receiptFormatter.BuildShiftReport(report, settings);

        _db.ShiftReports.Add(new ShiftReport
        {
            ShiftId = shift.Id,
            ReportType = ShiftReportType.ZReport,
            GeneratedAtUtc = report.GeneratedAtUtc,
            GeneratedBy = report.CashierName,
            InvoiceCount = report.InvoiceCount,
            TotalSales = report.TotalSales,
            TotalDiscount = report.TotalDiscount,
            TotalTax = report.TotalTax,
            TotalCashSales = report.TotalCashSales,
            TotalCardSales = report.TotalCardSales,
            TotalWalletSales = report.TotalWalletSales,
            TotalCreditSales = report.TotalCreditSales,
            TotalRefunds = report.TotalRefunds,
            OpeningCash = report.OpeningCash,
            ExpectedCash = report.ExpectedCash,
            ClosingCash = report.ClosingCash,
            CashDifference = report.CashDifference,
            PrintedContent = printedContent
        });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shift {ShiftNumber} closed: expected {Expected}, counted {Counted}, difference {Difference}",
            shift.ShiftNumber, report.ExpectedCash, report.ClosingCash, report.CashDifference);

        return report;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShiftReportDto>> GetReportsAsync(int shiftId, CancellationToken cancellationToken = default)
    {
        return await _db.ShiftReports
            .AsNoTracking()
            .Where(r => r.ShiftId == shiftId)
            .OrderByDescending(r => r.GeneratedAtUtc)
            .Select(r => new ShiftReportDto
            {
                ShiftId = r.ShiftId,
                ShiftNumber = r.Shift != null ? r.Shift.ShiftNumber : string.Empty,
                CashierName = r.GeneratedBy ?? string.Empty,
                TerminalId = r.Shift != null ? r.Shift.TerminalId : string.Empty,
                ReportType = r.ReportType,
                GeneratedAtUtc = r.GeneratedAtUtc,
                OpenedAtUtc = r.Shift != null ? r.Shift.OpenedAtUtc : r.GeneratedAtUtc,
                ClosedAtUtc = r.Shift != null ? r.Shift.ClosedAtUtc : null,
                OpeningCash = r.OpeningCash,
                ExpectedCash = r.ExpectedCash,
                ClosingCash = r.ClosingCash,
                CashDifference = r.CashDifference,
                TotalSales = r.TotalSales,
                TotalCashSales = r.TotalCashSales,
                TotalCardSales = r.TotalCardSales,
                TotalWalletSales = r.TotalWalletSales,
                TotalCreditSales = r.TotalCreditSales,
                TotalDiscount = r.TotalDiscount,
                TotalTax = r.TotalTax,
                TotalRefunds = r.TotalRefunds,
                InvoiceCount = r.InvoiceCount
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShiftDto>> GetShiftsAsync(
        DateTime fromDate,
        DateTime toDate,
        string? terminalId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var fromUtc = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var query = _db.Shifts
            .AsNoTracking()
            .Where(s => s.OpenedAtUtc >= fromUtc && s.OpenedAtUtc < toUtc);

        if (!string.IsNullOrWhiteSpace(terminalId))
        {
            query = query.Where(s => s.TerminalId == terminalId);
        }

        return await query
            .OrderByDescending(s => s.OpenedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(s => new ShiftDto
            {
                Id = s.Id,
                ShiftNumber = s.ShiftNumber,
                UserId = s.UserId,
                CashierName = s.User != null ? s.User.FullName : string.Empty,
                TerminalId = s.TerminalId,
                OpenedAtUtc = s.OpenedAtUtc,
                ClosedAtUtc = s.ClosedAtUtc,
                Status = s.Status,
                OpeningCash = s.OpeningCash,
                ClosingCash = s.ClosingCash,
                ExpectedCash = s.ExpectedCash,
                CashDifference = s.CashDifference,
                TotalSales = s.TotalSales,
                TotalCashSales = s.TotalCashSales,
                TotalCardSales = s.TotalCardSales,
                TotalWalletSales = s.TotalWalletSales,
                TotalCreditSales = s.TotalCreditSales,
                TotalDiscount = s.TotalDiscount,
                TotalTax = s.TotalTax,
                InvoiceCount = s.InvoiceCount
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Aggregates the shift from its invoices. Used by both the X report (nothing persisted) and the
    /// Z report (persisted on closing).
    /// </summary>
    private async Task<ShiftReportDto> BuildReportCoreAsync(Shift shift, ShiftReportType reportType, CancellationToken cancellationToken)
    {
        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.ShiftId == shift.Id)
            .Select(i => new
            {
                i.Status,
                i.PaymentMethod,
                i.TotalAmount,
                i.DiscountAmount,
                i.TaxAmount,
                i.PaidAmount
            })
            .ToListAsync(cancellationToken);

        var completed = invoices.Where(i => i.Status == InvoiceStatus.Completed).ToList();
        var refunded = invoices
            .Where(i => i.Status == InvoiceStatus.Refunded || i.Status == InvoiceStatus.Cancelled)
            .ToList();

        decimal SumBy(PaymentMethod method) =>
            Math.Round(completed.Where(i => i.PaymentMethod == method).Sum(i => i.TotalAmount), 2);

        var totalCash = SumBy(PaymentMethod.Cash);
        var totalRefunds = Math.Round(refunded.Sum(i => i.TotalAmount), 2);

        // Refunds are paid back in cash, therefore they reduce the expected drawer contents.
        var expectedCash = Math.Round(shift.OpeningCash + totalCash - totalRefunds, 2);

        return new ShiftReportDto
        {
            ShiftId = shift.Id,
            ShiftNumber = shift.ShiftNumber,
            CashierName = shift.User?.FullName ?? string.Empty,
            TerminalId = shift.TerminalId,
            ReportType = reportType,
            GeneratedAtUtc = _clock.UtcNow,
            OpenedAtUtc = shift.OpenedAtUtc,
            ClosedAtUtc = shift.ClosedAtUtc,
            OpeningCash = shift.OpeningCash,
            ExpectedCash = expectedCash,
            ClosingCash = shift.ClosingCash,
            CashDifference = shift.CashDifference,
            TotalSales = Math.Round(completed.Sum(i => i.TotalAmount), 2),
            TotalCashSales = totalCash,
            TotalCardSales = SumBy(PaymentMethod.Card),
            TotalWalletSales = SumBy(PaymentMethod.Wallet),
            TotalCreditSales = SumBy(PaymentMethod.Credit),
            TotalDiscount = Math.Round(completed.Sum(i => i.DiscountAmount), 2),
            TotalTax = Math.Round(completed.Sum(i => i.TaxAmount), 2),
            TotalRefunds = totalRefunds,
            InvoiceCount = completed.Count,
            CancelledInvoiceCount = refunded.Count
        };
    }
}
