using System.ComponentModel.DataAnnotations;
using Supermarket.Core.Common;

namespace Supermarket.Core.Entities;

/// <summary>
/// Product category tree. A category may have a parent to build two-level menus
/// (e.g. "ألبان" -> "جبن").
/// </summary>
public class Category : BaseEntity
{
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? ParentCategoryId { get; set; }

    public Category? ParentCategory { get; set; }

    public ICollection<Category> SubCategories { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();

    /// <summary>Sort order used by the touch-screen category bar.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Measurement unit (قطعة، كيلوجرام، لتر ...). <see cref="AllowFractions"/> decides whether
/// the POS may sell decimal quantities for the products that use this unit.
/// </summary>
public class Unit : BaseEntity
{
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Symbol { get; set; }

    public bool AllowFractions { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>
/// Supplier / vendor master data.
/// </summary>
public class Supplier : BaseEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? ContactPerson { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(400)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? TaxNumber { get; set; }

    /// <summary>Balance owed to the supplier (positive = we owe the supplier).</summary>
    public decimal Balance { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}

/// <summary>
/// Customer record, created mainly for credit invoices and loyalty points.
/// </summary>
public class Customer : BaseEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    /// <summary>Outstanding debt for credit sales (positive = customer owes money).</summary>
    public decimal Balance { get; set; }

    /// <summary>Loyalty points collected from completed invoices.</summary>
    public int LoyaltyPoints { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
