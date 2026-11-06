namespace Supermarket.Core.DTOs;

/// <summary>
/// Filter object bound to the admin / POS search bars.
/// </summary>
public sealed class ProductFilter
{
    /// <summary>Free text matched against the Arabic name, the barcode and the description.</summary>
    public string? SearchTerm { get; set; }

    public int? CategoryId { get; set; }

    public int? SupplierId { get; set; }

    public int? UnitId { get; set; }

    /// <summary>When true only products at or below their alert threshold are returned.</summary>
    public bool LowStockOnly { get; set; }

    /// <summary>When true products with zero quantity are returned.</summary>
    public bool OutOfStockOnly { get; set; }

    /// <summary>Null means "both active and inactive".</summary>
    public bool? IsActive { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    /// <summary>Sort column: Name, Barcode, StockQuantity, SellingPrice, CreatedAtUtc.</summary>
    public string SortBy { get; set; } = "Name";

    public bool SortDescending { get; set; }
}

/// <summary>
/// Editable payload used when creating or updating a product from the admin form.
/// </summary>
public sealed class ProductCreateUpdateDto
{
    /// <summary>Zero when a new product is being created.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Left empty to let the service generate the next internal barcode.</summary>
    public string? Barcode { get; set; }

    public string? AlternativeBarcode { get; set; }

    public int CategoryId { get; set; }

    public int? SupplierId { get; set; }

    public int UnitId { get; set; }

    public decimal CostPrice { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal? DiscountPrice { get; set; }

    public decimal VatRate { get; set; }

    public decimal MinimumStockLevel { get; set; }

    public decimal ReorderQuantity { get; set; }

    /// <summary>Opening quantity; only applied on creation (later changes go through the stock service).</summary>
    public decimal InitialStock { get; set; }

    public string? ShelfLocation { get; set; }

    public bool IsWeighted { get; set; }

    public bool IsActive { get; set; } = true;

    public string? ImagePath { get; set; }

    /// <summary>Operator name written to the price history rows.</summary>
    public string? ChangedBy { get; set; }
}

/// <summary>
/// One line of the POS cart (Phase 3) but already part of the shared contract so that the
/// sales service can be unit tested before the touch screen exists.
/// </summary>
public sealed class CartLineDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public decimal UnitCost { get; set; }

    public decimal Quantity { get; set; } = 1m;

    public decimal DiscountAmount { get; set; }

    public decimal VatRate { get; set; }

    public decimal LineTotal => Math.Round(Quantity * UnitPrice - DiscountAmount, 2);

    public decimal TaxAmount => Math.Round(LineTotal * VatRate / 100m, 2);

    public decimal LineTotalWithTax => Math.Round(LineTotal + TaxAmount, 2);
}

/// <summary>
/// Payload handed to <c>ISalesService.CompleteSaleAsync</c> by the POS checkout dialog.
/// </summary>
public sealed class SaleRequestDto
{
    public int UserId { get; set; }

    public int? ShiftId { get; set; }

    public int? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public IReadOnlyList<CartLineDto> Lines { get; set; } = Array.Empty<CartLineDto>();

    /// <summary>Invoice level discount amount (applied after line discounts).</summary>
    public decimal InvoiceDiscount { get; set; }

    /// <summary>Invoice level discount percentage; ignored when <see cref="InvoiceDiscount"/> is set.</summary>
    public decimal InvoiceDiscountPercentage { get; set; }

    public decimal PaidAmount { get; set; }

    public Common.PaymentMethod PaymentMethod { get; set; } = Common.PaymentMethod.Cash;

    public string? Notes { get; set; }
}

/// <summary>
/// Live totals of a cart while it is still being built (POS totals panel).
/// </summary>
public sealed class CartPricingDto
{
    /// <summary>Sum of the line totals before any discount and before tax.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Sum of the line level discounts already reflected in <see cref="SubTotal"/>.</summary>
    public decimal LineDiscountTotal { get; set; }

    /// <summary>Invoice level discount amount.</summary>
    public decimal InvoiceDiscount { get; set; }

    /// <summary>Discount percentage applied at invoice level (informative).</summary>
    public decimal InvoiceDiscountPercentage { get; set; }

    /// <summary>VAT amount.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Grand total payable.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Total after removing the VAT portion (الصافي).</summary>
    public decimal NetTotal => TotalAmount - TaxAmount;

    /// <summary>Total number of units in the cart (weighted lines contribute their decimal quantity).</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>Number of distinct lines in the cart.</summary>
    public int LineCount { get; set; }

    public decimal TotalDiscount => LineDiscountTotal + InvoiceDiscount;
}

/// <summary>Outcome of a completed checkout, consumed by the thermal printer service.</summary>
public sealed class SaleResultDto
{
    public int InvoiceId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDateUtc { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal ChangeAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public Common.PaymentMethod PaymentMethod { get; set; }

    public IReadOnlyList<CartLineDto> Lines { get; set; } = Array.Empty<CartLineDto>();

    public string CashierName { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public string? ShiftNumber { get; set; }
}
