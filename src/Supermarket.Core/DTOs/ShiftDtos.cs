using Supermarket.Core.Common;

namespace Supermarket.Core.DTOs;

/// <summary>Shift projection for the POS header and the admin shift list.</summary>
public sealed class ShiftDto
{
    public int Id { get; set; }

    public string ShiftNumber { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public string TerminalId { get; set; } = string.Empty;

    public DateTime OpenedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public Common.ShiftStatus Status { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal? ClosingCash { get; set; }

    public decimal ExpectedCash { get; set; }

    public decimal CashDifference { get; set; }

    public decimal TotalSales { get; set; }

    public decimal TotalCashSales { get; set; }

    public decimal TotalCardSales { get; set; }

    public decimal TotalWalletSales { get; set; }

    public decimal TotalCreditSales { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TotalTax { get; set; }

    public int InvoiceCount { get; set; }

    public string StatusArabic => Status.ToArabic();
}

/// <summary>
/// Aggregated figures produced by <c>IShiftService.BuildReportAsync</c>.
/// Used for both the X report (peek) and the Z report (closing).
/// </summary>
public sealed class ShiftReportDto
{
    public int ShiftId { get; set; }

    public string ShiftNumber { get; set; } = string.Empty;

    public string CashierName { get; set; } = string.Empty;

    public string TerminalId { get; set; } = string.Empty;

    public Common.ShiftReportType ReportType { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public DateTime OpenedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal ExpectedCash { get; set; }

    public decimal? ClosingCash { get; set; }

    public decimal CashDifference { get; set; }

    public decimal TotalSales { get; set; }

    public decimal TotalCashSales { get; set; }

    public decimal TotalCardSales { get; set; }

    public decimal TotalWalletSales { get; set; }

    public decimal TotalCreditSales { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalRefunds { get; set; }

    public int InvoiceCount { get; set; }

    public int CancelledInvoiceCount { get; set; }

    public decimal AverageInvoiceValue => InvoiceCount == 0 ? 0m : Math.Round(TotalSales / InvoiceCount, 2);

    /// <summary>Cash the cashier must hand over: expected cash minus nothing (refunds already deducted).</summary>
    public decimal CashToHandOver => ExpectedCash;
}

/// <summary>Request used by the POS when the cashier closes the shift.</summary>
public sealed class ShiftCloseRequestDto
{
    public int ShiftId { get; set; }

    /// <summary>Cash physically counted in the drawer.</summary>
    public decimal ClosingCash { get; set; }

    public string? Notes { get; set; }

    /// <summary>Supervisor who performed the closing.</summary>
    public string? ClosedBy { get; set; }
}

/// <summary>Request used when a cashier opens a new shift.</summary>
public sealed class ShiftOpenRequestDto
{
    public int UserId { get; set; }

    public string TerminalId { get; set; } = "POS-01";

    public decimal OpeningCash { get; set; }

    public string? Notes { get; set; }
}

/// <summary>User projection used by the role management screen (Phase 5).</summary>
public sealed class UserDto
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public Common.UserRole Role { get; set; }

    public string RoleArabic => Role.ToArabic();

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }
}

/// <summary>Result of an authentication attempt.</summary>
public sealed class AuthResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public UserDto? User { get; init; }

    public static AuthResult Success(UserDto user) => new() { Succeeded = true, User = user };

    public static AuthResult Failure(string message) => new() { Succeeded = false, ErrorMessage = message };
}
