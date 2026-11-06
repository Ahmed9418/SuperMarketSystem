using Supermarket.Core.Common;

namespace Supermarket.Core.DTOs;

// ---------------------------------------------------------------------------------------------
// Phase 5 reporting contracts. Every DTO here is a projection: the services never hand entities to
// the charts or the grids, so nothing is ever change-tracked by the reporting screens.
// ---------------------------------------------------------------------------------------------

/// <summary>Date range + optional filters shared by every report screen.</summary>
public sealed class ReportFilter
{
    public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-29);

    public DateTime ToDate { get; set; } = DateTime.Today;

    /// <summary>Null = all cashiers.</summary>
    public int? UserId { get; set; }

    /// <summary>Null = all payment methods.</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    public bool IsValid => FromDate.Date <= ToDate.Date;

    /// <summary>Normalises the range to whole days; the query converts to UTC by itself.</summary>
    public ReportFilter Normalize()
    {
        if (FromDate.Date > ToDate.Date)
        {
            (FromDate, ToDate) = (ToDate, FromDate);
        }

        FromDate = FromDate.Date;
        ToDate = ToDate.Date;
        return this;
    }

    public int DayCount => Math.Max(1, (ToDate.Date - FromDate.Date).Days + 1);
}

/// <summary>One row of the daily sales series used by the charts.</summary>
public sealed class SalesTrendPointDto
{
    public DateTime Date { get; set; }

    public decimal Sales { get; set; }

    public decimal Profit { get; set; }

    public int InvoiceCount { get; set; }

    public decimal AverageInvoice => InvoiceCount == 0 ? 0m : Math.Round(Sales / InvoiceCount, 2);

    /// <summary>Short Arabic label for the chart axis, e.g. "05/11".</summary>
    public string Label => Date.ToString("dd/MM");
}

/// <summary>Payment method split of a period (pie chart + table).</summary>
public sealed class PaymentBreakdownDto
{
    public PaymentMethod Method { get; set; }

    public string MethodName => Method.ToArabic();

    public decimal Amount { get; set; }

    public int InvoiceCount { get; set; }

    public decimal Share { get; set; }

    public string ShareText => $"{Share:0.0}%";
}

/// <summary>Takings of one cashier in a period.</summary>
public sealed class CashierPerformanceDto
{
    public int UserId { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public int InvoiceCount { get; set; }

    public decimal Sales { get; set; }

    public decimal Profit { get; set; }

    public decimal AverageInvoice { get; set; }

    public int ShiftCount { get; set; }

    /// <summary>Difference between counted and expected cash, summed over the closed shifts.</summary>
    public decimal CashDifference { get; set; }
}

/// <summary>Sales of one category in a period (column chart).</summary>
public sealed class CategorySalesDto
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public decimal Sales { get; set; }

    public decimal Profit { get; set; }

    public decimal Quantity { get; set; }

    public int LineCount { get; set; }
}

/// <summary>Stock value / low stock of one category.</summary>
public sealed class CategoryStockDto
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public int ProductCount { get; set; }

    public int LowStockCount { get; set; }

    public decimal Quantity { get; set; }

    public decimal StockValue { get; set; }
}

/// <summary>
/// The complete sales analytics page in one round trip: KPI tiles, chart series and the tables.
/// </summary>
public sealed class SalesAnalyticsDto
{
    public ReportFilter Filter { get; set; } = new();

    // ------------------------------------------------------------------ KPI tiles
    public decimal GrossSales { get; set; }

    public decimal NetSales { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalProfit { get; set; }

    public int InvoiceCount { get; set; }

    public int CancelledInvoiceCount { get; set; }

    public int RefundedInvoiceCount { get; set; }

    public int ItemsSold { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal CreditSales { get; set; }

    public decimal AverageInvoice => InvoiceCount == 0 ? 0m : Math.Round(NetSales / InvoiceCount, 2);

    public decimal ProfitMargin => NetSales == 0m ? 0m : Math.Round(TotalProfit / NetSales * 100m, 2);

    /// <summary>Number of shifts that were closed inside the period.</summary>
    public int ClosedShifts { get; set; }

    /// <summary>Sum of the shortages / surpluses of those shifts (negative = shortage).</summary>
    public decimal CashDifference { get; set; }

    // ------------------------------------------------------------------ Series
    public IReadOnlyList<SalesTrendPointDto> DailySales { get; set; } = Array.Empty<SalesTrendPointDto>();

    public IReadOnlyList<PaymentBreakdownDto> PaymentBreakdown { get; set; } = Array.Empty<PaymentBreakdownDto>();

    public IReadOnlyList<CategorySalesDto> CategorySales { get; set; } = Array.Empty<CategorySalesDto>();

    public IReadOnlyList<TopProductDto> TopProducts { get; set; } = Array.Empty<TopProductDto>();

    public IReadOnlyList<CashierPerformanceDto> Cashiers { get; set; } = Array.Empty<CashierPerformanceDto>();

    public IReadOnlyList<CategoryStockDto> CategoryStock { get; set; } = Array.Empty<CategoryStockDto>();

    /// <summary>Same period one step earlier, used for the "compared to previous period" badge.</summary>
    public decimal PreviousPeriodSales { get; set; }

    public decimal SalesGrowth => PreviousPeriodSales == 0m
        ? (NetSales > 0m ? 100m : 0m)
        : Math.Round((NetSales - PreviousPeriodSales) / PreviousPeriodSales * 100m, 1);

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>One invoice row of the invoice browser / report grid.</summary>
public sealed class InvoiceListDto
{
    public int Id { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDateUtc { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public string ShiftNumber { get; set; } = string.Empty;

    public int ItemCount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string PaymentMethodName => PaymentMethod.ToArabic();

    public InvoiceStatus Status { get; set; }

    public string StatusName => Status.ToArabic();

    public bool IsCancelled => Status == InvoiceStatus.Cancelled;

    public bool IsCredit => PaymentMethod == PaymentMethod.Credit;
}

/// <summary>Filters for the invoice browser.</summary>
public sealed class InvoiceFilter
{
    public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-7);

    public DateTime ToDate { get; set; } = DateTime.Today;

    public string? SearchTerm { get; set; }

    public int? UserId { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    public InvoiceStatus? Status { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

/// <summary>One line of a stored invoice, used by the invoice detail panel.</summary>
public sealed class InvoiceLineDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public decimal LineTotalWithTax { get; set; }

    public decimal Profit { get; set; }
}

/// <summary>An invoice with its lines, for the detail panel / reprint.</summary>
public sealed class InvoiceDetailDto
{
    public InvoiceListDto Header { get; set; } = new();

    public IReadOnlyList<InvoiceLineDto> Lines { get; set; } = Array.Empty<InvoiceLineDto>();

    public string? Notes { get; set; }

    public decimal SubTotal { get; set; }

    public decimal ChangeAmount { get; set; }
}
