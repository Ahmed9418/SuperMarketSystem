using System.ComponentModel.DataAnnotations;
using Supermarket.Core.Common;

namespace Supermarket.Core.Entities;

/// <summary>
/// Application user. The password is stored as a PBKDF2 hash produced by
/// <c>PasswordHasher&lt;User&gt;</c>; plain text passwords are never persisted.
/// </summary>
public class User : BaseEntity
{
    [MaxLength(80)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Arabic full name shown in the header and on the receipts.</summary>
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Cashier;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>Optional 4-digit PIN used for the fast POS login.</summary>
    [MaxLength(200)]
    public string? PinHash { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    /// <summary>Per-user permission overrides on top of the role defaults.</summary>
    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
}

/// <summary>
/// Granular permission override for a single user. A missing row means "use the role default".
/// </summary>
public class UserPermission : BaseEntity
{
    public int UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Permission key, e.g. "Products.Edit" or "Reports.View".</summary>
    [MaxLength(80)]
    public string PermissionKey { get; set; } = string.Empty;

    public bool IsGranted { get; set; }
}

/// <summary>
/// Store / company settings rendered on the thermal receipt header.
/// A single row is expected (Id = 1).
/// </summary>
public class StoreSetting : BaseEntity
{
    [MaxLength(200)]
    public string StoreName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MaxLength(50)]
    public string? TaxNumber { get; set; }

    [MaxLength(400)]
    public string? ReceiptFooterMessage { get; set; }

    /// <summary>Printer name as registered in Windows; empty means "use the default printer".</summary>
    [MaxLength(200)]
    public string? ReceiptPrinterName { get; set; }

    /// <summary>Characters per line for the 80mm paper (usually 42 or 48).</summary>
    public int ReceiptLineWidth { get; set; } = 42;

    public decimal DefaultVatRate { get; set; } = DomainConstants.DefaultVatRate;

    /// <summary>When true prices already include VAT and the receipt shows a tax breakdown.</summary>
    public bool PricesIncludeVat { get; set; } = true;

    /// <summary>Number of decimals shown on receipts.</summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>Automatically creates the next barcode when the admin leaves the field empty.</summary>
    public bool AutoGenerateBarcode { get; set; } = true;

    /// <summary>Paper size for receipts/invoices: "80mm", "A4", or "A5".</summary>
    [MaxLength(20)]
    public string InvoicePaperSize { get; set; } = "80mm";

    /// <summary>Absolute path or local URI of the store logo displayed on receipts/invoices.</summary>
    [MaxLength(500)]
    public string? LogoPath { get; set; }
}
