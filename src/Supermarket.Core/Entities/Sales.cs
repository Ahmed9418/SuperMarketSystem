using System.ComponentModel.DataAnnotations;
using Supermarket.Core.Common;

namespace Supermarket.Core.Entities;

/// <summary>
/// A POS invoice (فاتورة بيع). One invoice belongs to exactly one cashier shift.
/// </summary>
public class Invoice : BaseEntity
{
    /// <summary>Human readable invoice number, e.g. "INV-20261105-0001".</summary>
    [MaxLength(40)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow;

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Cashier who created the invoice.</summary>
    public int UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Shift that the invoice was rung on. Required for shift closing reports.</summary>
    public int? ShiftId { get; set; }

    public Shift? Shift { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Completed;

    /// <summary>Sum of line totals before discounts and taxes.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Total invoice level discount amount (not including line discounts).</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Discount percentage applied at invoice level.</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>VAT amount included in <see cref="TotalAmount"/>.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Grand total payable by the customer.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Amount the cashier actually collected.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Amount handed back to the customer (المتبقي).</summary>
    public decimal ChangeAmount { get; set; }

    /// <summary>Unpaid remainder for credit invoices.</summary>
    public decimal RemainingAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [MaxLength(40)]
    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    /// <summary>True when the invoice was produced without a registered customer.</summary>
    public bool IsWalkIn { get; set; } = true;

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    /// <summary>Net total (الصافي) after discounts and taxes.</summary>
    public decimal NetTotal => TotalAmount - TaxAmount;
}

/// <summary>A single sold line.</summary>
public class InvoiceItem : BaseEntity
{
    public int InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Snapshot of the Arabic name at sale time (immunity against later renames).</summary>
    [MaxLength(DomainConstants.ProductNameMaxLength)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(DomainConstants.BarcodeMaxLength)]
    public string? Barcode { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Unit price at sale time.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Unit cost at sale time, copied for profit reporting.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Line level discount amount.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>VAT rate applied to the line.</summary>
    public decimal VatRate { get; set; }

    /// <summary>VAT amount for the line.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Quantity * UnitPrice - DiscountAmount (before tax).</summary>
    public decimal LineTotal { get; set; }

    /// <summary>LineTotal + TaxAmount.</summary>
    public decimal LineTotalWithTax { get; set; }

    /// <summary>Profit of the line: (UnitPrice - UnitCost) * Quantity - DiscountAmount.</summary>
    public decimal LineProfit => (UnitPrice - UnitCost) * Quantity - DiscountAmount;
}

/// <summary>
/// One tender (payment) against an invoice. Supports split payments in later phases.
/// </summary>
public class Payment : BaseEntity
{
    public int InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(60)]
    public string? TransactionReference { get; set; }
}
