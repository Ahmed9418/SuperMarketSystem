namespace Supermarket.Core.DTOs;

/// <summary>
/// Lightweight product projection used by the grids of both applications.
/// Never bind entities directly to the UI: change tracking must stay inside the service layer.
/// </summary>
public sealed class ProductDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string? AlternativeBarcode { get; set; }

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public int? SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public int UnitId { get; set; }

    public string UnitName { get; set; } = string.Empty;

    public decimal CostPrice { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal? DiscountPrice { get; set; }

    public decimal EffectivePrice => DiscountPrice is > 0m && DiscountPrice < SellingPrice
        ? DiscountPrice.Value
        : SellingPrice;

    public decimal VatRate { get; set; }

    public decimal StockQuantity { get; set; }

    public decimal MinimumStockLevel { get; set; }

    public decimal ReorderQuantity { get; set; }

    public string? ShelfLocation { get; set; }

    public bool IsWeighted { get; set; }

    public bool IsActive { get; set; }

    public string? ImagePath { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Stock level at or below the alert threshold.</summary>
    public bool IsLowStock => IsActive && StockQuantity <= MinimumStockLevel;

    /// <summary>True when the product is completely out of stock.</summary>
    public bool IsOutOfStock => StockQuantity <= 0m;

    /// <summary>Gross margin based on the effective selling price.</summary>
    public decimal Margin => EffectivePrice - CostPrice;

    /// <summary>Margin expressed as a percentage of the selling price.</summary>
    public decimal MarginPercentage => EffectivePrice <= 0m ? 0m : Math.Round(Margin / EffectivePrice * 100m, 2);

    /// <summary>Total value of the on-hand quantity at cost price.</summary>
    public decimal StockValue => Math.Round(StockQuantity * CostPrice, 2);
}

/// <summary>
/// Minimal lookup projection (id + Arabic display name) used by combo boxes.
/// </summary>
public sealed class LookupDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional additional text, e.g. the unit symbol or the supplier phone.</summary>
    public string? Description { get; set; }
}

/// <summary>Supplier projection for the admin grid.</summary>
public sealed class SupplierDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? TaxNumber { get; set; }

    public decimal Balance { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    public int ProductCount { get; set; }
}

/// <summary>Result of a single barcode scan handled by the POS.</summary>
public sealed class ProductLookupResultDto
{
    public bool Found { get; set; }

    /// <summary>Arabic message ready to be shown on the POS status bar.</summary>
    public string Message { get; set; } = string.Empty;

    public ProductDto? Product { get; set; }

    public static ProductLookupResultDto NotFound(string barcode) => new()
    {
        Found = false,
        Message = $"البار كود ({barcode}) غير مسجل بالنظام"
    };
}

/// <summary>Dashboard counters rendered on the admin home screen.</summary>
public sealed class DashboardSummaryDto
{
    public int TotalProducts { get; set; }

    public int ActiveProducts { get; set; }

    public int LowStockProducts { get; set; }

    public int OutOfStockProducts { get; set; }

    public int TotalCategories { get; set; }

    public int TotalSuppliers { get; set; }

    public int TotalUsers { get; set; }

    public decimal TotalStockValue { get; set; }

    public decimal TodaySales { get; set; }

    public int TodayInvoiceCount { get; set; }

    public decimal MonthSales { get; set; }

    public decimal MonthProfit { get; set; }

    public int OpenShifts { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Daily sales aggregate used by the Phase 5 charts.</summary>
public sealed class DailySalesDto
{
    public DateTime Date { get; set; }

    public decimal TotalSales { get; set; }

    public decimal TotalProfit { get; set; }

    public int InvoiceCount { get; set; }
}

/// <summary>Best selling product aggregate used by the Phase 5 reports.</summary>
public sealed class TopProductDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal QuantitySold { get; set; }

    public decimal Revenue { get; set; }

    public decimal Profit { get; set; }
}
