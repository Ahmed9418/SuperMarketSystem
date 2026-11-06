using System.ComponentModel.DataAnnotations;
using Supermarket.Core.Common;

namespace Supermarket.Core.Entities;

/// <summary>
/// Master data of a sellable item.
/// </summary>
public class Product : BaseEntity
{
    /// <summary>Arabic product name shown on the POS grid and on the thermal receipt.</summary>
    [MaxLength(DomainConstants.ProductNameMaxLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short description / size, e.g. "1 لتر" or "عبوة 500 جم".</summary>
    public string? Description { get; set; }

    /// <summary>Scanned barcode (EAN-13, EAN-8 or an internally generated code).</summary>
    [MaxLength(DomainConstants.BarcodeMaxLength)]
    public string Barcode { get; set; } = string.Empty;

    /// <summary>Optional second barcode for suppliers that ship with their own codes.</summary>
    [MaxLength(DomainConstants.BarcodeMaxLength)]
    public string? AlternativeBarcode { get; set; }

    /// <summary>Category foreign key.</summary>
    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>Main supplier foreign key (a product may also be purchased from others).</summary>
    public int? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>Measurement unit foreign key.</summary>
    public int UnitId { get; set; }

    public Unit? Unit { get; set; }

    /// <summary>Cost price used for profit calculations, stored with 4 decimals.</summary>
    public decimal CostPrice { get; set; }

    /// <summary>Selling price, stored with 4 decimals, displayed with 2.</summary>
    public decimal SellingPrice { get; set; }

    /// <summary>Optional promotional price. When set and active it overrides <see cref="SellingPrice"/>.</summary>
    public decimal? DiscountPrice { get; set; }

    /// <summary>VAT percentage for this product (0.14 = 14%).</summary>
    public decimal VatRate { get; set; }

    /// <summary>Current on-hand quantity across all stock (kept in sync by the stock service).</summary>
    public decimal StockQuantity { get; set; }

    /// <summary>Quantity at which the low-stock alert is raised.</summary>
    public decimal MinimumStockLevel { get; set; }

    /// <summary>Preferred reorder quantity used by the purchase suggestion report.</summary>
    public decimal ReorderQuantity { get; set; }

    /// <summary>Storage location inside the store, e.g. "رف 4 - ممر 2".</summary>
    public string? ShelfLocation { get; set; }

    /// <summary>True when the item is weighed at the counter (cheese, meat, vegetables).</summary>
    public bool IsWeighted { get; set; }

    /// <summary>False for discontinued items; they disappear from the POS search results.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional image shown on the touch screen tiles.</summary>
    public string? ImagePath { get; set; }

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();

    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    /// <summary>True when the item has reached its alert threshold.</summary>
    public bool IsLowStock => IsActive && StockQuantity <= MinimumStockLevel;

    /// <summary>Effective price after applying an active promotional price.</summary>
    public decimal EffectivePrice => DiscountPrice is > 0m && DiscountPrice < SellingPrice ? DiscountPrice.Value : SellingPrice;

    /// <summary>Profit per unit based on the effective price.</summary>
    public decimal UnitMargin => EffectivePrice - CostPrice;
}
