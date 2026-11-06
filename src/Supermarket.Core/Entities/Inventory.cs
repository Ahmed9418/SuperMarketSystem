using System.ComponentModel.DataAnnotations;
using Supermarket.Core.Common;

namespace Supermarket.Core.Entities;

/// <summary>
/// Immutable stock ledger row. Every quantity change inside the system must be written
/// through <c>IStockService</c> so that this ledger always reconciles with
/// <see cref="Product.StockQuantity"/>.
/// </summary>
public class StockMovement : BaseEntity
{
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public StockMovementType MovementType { get; set; }

    /// <summary>Signed quantity: positive for inbound, negative for outbound movements.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Quantity on hand immediately after this movement was applied.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>Unit cost applied to this movement (used by the valuation report).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Reference to the business document that caused the movement.</summary>
    [MaxLength(60)]
    public string? ReferenceNumber { get; set; }

    /// <summary>Related invoice id, when the movement came from a sale.</summary>
    public int? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    /// <summary>Related purchase order id, when the movement came from a supply.</summary>
    public int? PurchaseOrderId { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>Related shift id, when the movement came from a POS sale.</summary>
    public int? ShiftId { get; set; }

    public Shift? Shift { get; set; }

    [MaxLength(400)]
    public string? Reason { get; set; }

    /// <summary>Operator who performed the movement.</summary>
    [MaxLength(120)]
    public string? PerformedBy { get; set; }
}

/// <summary>
/// Audit trail of selling / cost price changes so the admin can review margin erosion.
/// </summary>
public class PriceHistory : BaseEntity
{
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public decimal OldCostPrice { get; set; }

    public decimal NewCostPrice { get; set; }

    public decimal OldSellingPrice { get; set; }

    public decimal NewSellingPrice { get; set; }

    [MaxLength(400)]
    public string? Reason { get; set; }

    [MaxLength(120)]
    public string? ChangedBy { get; set; }
}

/// <summary>
/// Physical inventory (جرد) session. Lines are stored inside <see cref="StockCountItem"/>.
/// </summary>
public class StockCount : BaseEntity
{
    [MaxLength(40)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public DateTime CountDateUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(120)]
    public string? CountedBy { get; set; }

    /// <summary>True once the differences have been posted to the stock ledger.</summary>
    public bool IsPosted { get; set; }

    public string? Notes { get; set; }

    public ICollection<StockCountItem> Items { get; set; } = new List<StockCountItem>();
}

/// <summary>One counted product inside a <see cref="StockCount"/> session.</summary>
public class StockCountItem : BaseEntity
{
    public int StockCountId { get; set; }

    public StockCount? StockCount { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Quantity before the count according to the system.</summary>
    public decimal SystemQuantity { get; set; }

    /// <summary>Quantity physically counted by the stock keeper.</summary>
    public decimal CountedQuantity { get; set; }

    /// <summary>Counted minus system quantity.</summary>
    public decimal Difference => CountedQuantity - SystemQuantity;
}
