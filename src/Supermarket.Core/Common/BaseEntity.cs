namespace Supermarket.Core.Common;

/// <summary>
/// Base class for every persisted entity. Provides the surrogate primary key and the
/// audit metadata that is stamped automatically by <c>SupermarketDbContext</c>.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Surrogate primary key (identity column).</summary>
    public int Id { get; set; }

    /// <summary>UTC timestamp of the insert operation.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update operation.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>User name of the operator who created the record.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>User name of the operator who performed the last update.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Soft-delete flag. Master data is never physically removed so that historical
    /// invoices keep resolving their product / supplier references.
    /// </summary>
    public bool IsDeleted { get; set; }
}
