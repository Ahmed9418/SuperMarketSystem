using System.ComponentModel.DataAnnotations;
using Supermarket.Core.Common;

namespace Supermarket.Core.Entities;

/// <summary>
/// A cashier shift (ورديّة). The POS refuses to sell unless the signed-in cashier has an
/// <see cref="ShiftStatus.Open"/> shift. Closing a shift stores the Z-report figures.
/// </summary>
public class Shift : BaseEntity
{
    [MaxLength(40)]
    public string ShiftNumber { get; set; } = string.Empty;

    public int UserId { get; set; }

    public User? User { get; set; }

    /// <summary>POS terminal identifier (supports multi-terminal deployments).</summary>
    [MaxLength(60)]
    public string TerminalId { get; set; } = "POS-01";

    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAtUtc { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    /// <summary>Cash counted in the drawer when the shift started.</summary>
    public decimal OpeningCash { get; set; }

    /// <summary>Cash counted in the drawer when the shift was closed.</summary>
    public decimal? ClosingCash { get; set; }

    /// <summary>Cash the system expects in the drawer (opening + cash sales - refunds).</summary>
    public decimal ExpectedCash { get; set; }

    /// <summary>ClosingCash - ExpectedCash. Negative means shortage in the drawer.</summary>
    public decimal CashDifference { get; set; }

    public decimal TotalCashSales { get; set; }

    public decimal TotalCardSales { get; set; }

    public decimal TotalWalletSales { get; set; }

    public decimal TotalCreditSales { get; set; }

    public decimal TotalSales { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalRefunds { get; set; }

    public int InvoiceCount { get; set; }

    public string? Notes { get; set; }

    /// <summary>Operator who closed the shift (may be a supervisor instead of the cashier).</summary>
    [MaxLength(120)]
    public string? ClosedBy { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public ICollection<ShiftReport> Reports { get; set; } = new List<ShiftReport>();

    /// <summary>Duration of the shift, or of the open shift so far.</summary>
    public TimeSpan Duration => (ClosedAtUtc ?? DateTime.UtcNow) - OpenedAtUtc;
}

/// <summary>
/// Persisted X / Z report snapshot so that a closed shift can be reprinted at any time
/// with exactly the numbers that were printed originally.
/// </summary>
public class ShiftReport : BaseEntity
{
    public int ShiftId { get; set; }

    public Shift? Shift { get; set; }

    public ShiftReportType ReportType { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(120)]
    public string? GeneratedBy { get; set; }

    public int InvoiceCount { get; set; }

    public decimal TotalSales { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalCashSales { get; set; }

    public decimal TotalCardSales { get; set; }

    public decimal TotalWalletSales { get; set; }

    public decimal TotalCreditSales { get; set; }

    public decimal TotalRefunds { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal ExpectedCash { get; set; }

    public decimal? ClosingCash { get; set; }

    public decimal CashDifference { get; set; }

    /// <summary>Full 80mm receipt text so the report can be reprinted verbatim.</summary>
    public string? PrintedContent { get; set; }
}
