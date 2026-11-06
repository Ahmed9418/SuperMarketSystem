using Supermarket.Core.Common;

namespace Supermarket.Core.DTOs;

/// <summary>
/// Projection of one stock ledger row used by the movement history screen.
/// </summary>
public sealed class StockMovementDto
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public Common.StockMovementType MovementType { get; set; }

    /// <summary>Arabic label of <see cref="MovementType"/>.</summary>
    public string MovementTypeArabic => MovementType.ToArabic();

    /// <summary>Signed quantity: positive = inbound, negative = outbound.</summary>
    public decimal Quantity { get; set; }

    public decimal BalanceAfter { get; set; }

    public decimal UnitCost { get; set; }

    /// <summary>Value of the movement at cost price.</summary>
    public decimal MovementValue => Math.Round(Math.Abs(Quantity) * UnitCost, 2);

    public string? ReferenceNumber { get; set; }

    public string? Reason { get; set; }

    public string? PerformedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>True when the row increased the on-hand quantity.</summary>
    public bool IsInbound => Quantity > 0m;
}

/// <summary>Filters bound to the stock movement history screen.</summary>
public sealed class StockMovementFilter
{
    /// <summary>Free text matched against the product name / barcode.</summary>
    public string? SearchTerm { get; set; }

    public int? ProductId { get; set; }

    public Common.StockMovementType? MovementType { get; set; }

    /// <summary>Inclusive lower bound, local time.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Inclusive upper bound, local time (whole day).</summary>
    public DateTime? ToDate { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 100;
}

/// <summary>Direction of a manual stock correction.</summary>
public enum StockAdjustmentDirection
{
    /// <summary>Adds quantity (تسوية بالزيادة / مرتجع).</summary>
    Increase = 1,

    /// <summary>Removes quantity (تسوية بالنقص / تالف).</summary>
    Decrease = 2
}

/// <summary>
/// Payload of a manual stock correction entered by an authorised user.
/// The service validates that the movement type matches the direction.
/// </summary>
public sealed class StockAdjustmentRequest
{
    public int ProductId { get; set; }

    /// <summary>Always a positive amount; the direction decides the sign.</summary>
    public decimal Quantity { get; set; }

    public StockAdjustmentDirection Direction { get; set; }

    public Common.StockMovementType MovementType { get; set; }

    /// <summary>Mandatory business justification, written to the ledger.</summary>
    public string? Reason { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? PerformedBy { get; set; }
}

/// <summary>Category projection for the catalog management screen.</summary>
public sealed class CategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? ParentCategoryId { get; set; }

    public string? ParentCategoryName { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public int ProductCount { get; set; }
}

/// <summary>Unit projection for the catalog management screen.</summary>
public sealed class UnitDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Symbol { get; set; }

    /// <summary>True for grams / kilograms / litres, where the POS accepts decimal quantities.</summary>
    public bool AllowFractions { get; set; }

    public int ProductCount { get; set; }
}
