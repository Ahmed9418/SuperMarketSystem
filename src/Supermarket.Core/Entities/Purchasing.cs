using System.ComponentModel.DataAnnotations;
using Supermarket.Core.Common;

namespace Supermarket.Core.Entities;

/// <summary>
/// Purchase order (أمر توريد) raised against a supplier. Receiving an order writes
/// inbound <see cref="StockMovement"/> rows through the stock service.
/// </summary>
public class PurchaseOrder : BaseEntity
{
    [MaxLength(40)]
    public string OrderNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public int? UserId { get; set; }

    public User? User { get; set; }

    public DateTime OrderDateUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ExpectedDeliveryDateUtc { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingAmount => TotalAmount - PaidAmount;

    public string? Notes { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

/// <summary>Ordered line inside a <see cref="PurchaseOrder"/>.</summary>
public class PurchaseOrderItem : BaseEntity
{
    public int PurchaseOrderId { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public decimal OrderedQuantity { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>Quantity still to be delivered.</summary>
    public decimal PendingQuantity => OrderedQuantity - ReceivedQuantity;
}
