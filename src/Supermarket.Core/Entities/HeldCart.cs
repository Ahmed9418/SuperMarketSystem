using Supermarket.Core.Common;

namespace Supermarket.Core.Entities;

/// <summary>
/// A suspended (held) cart. The cashier parks a basket to serve the queue and resumes it from any
/// terminal: the payload is stored as JSON so the schema does not change when the cart line gains
/// fields in a later phase.
///
/// <para>
/// Held carts are operational data, not accounting data — resuming does not touch stock or the
/// shift, and only the final invoice does. A row is removed as soon as its basket is resumed or
/// cancelled.
/// </para>
/// </summary>
public class HeldCart : BaseEntity
{
    /// <summary>Human readable label shown in the resume list, e.g. the customer name.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Cashier who parked the basket.</summary>
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Shift the basket was parked in, so it can be audited.</summary>
    public int? ShiftId { get; set; }

    public Shift? Shift { get; set; }

    /// <summary>Terminal the basket was parked on.</summary>
    public string TerminalId { get; set; } = "POS-01";

    /// <summary>Serialized <c>HeldCartPayload</c> (product ids, quantities, discounts).</summary>
    public string CartJson { get; set; } = "{}";

    /// <summary>Number of lines, cached so the list does not have to parse the JSON.</summary>
    public int LineCount { get; set; }

    /// <summary>Total units, cached for the same reason.</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>Estimated total at the moment of holding (display only).</summary>
    public decimal EstimatedTotal { get; set; }
}
