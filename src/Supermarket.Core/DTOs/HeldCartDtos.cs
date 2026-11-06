using System.Text.Json;

namespace Supermarket.Core.DTOs;

/// <summary>
/// One suspended (held) cart: the cashier parks a basket to serve the next customer and comes back
/// to it later. Only the bare line data is kept — prices are re-read from the database on resume so
/// a price change never leaks into a parked basket.
/// </summary>
public sealed class HeldCartDto
{
    public int Id { get; set; }

    /// <summary>Short label the cashier sees, e.g. "العميل الذي طلب توصيل".</summary>
    public string Label { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public int ShiftId { get; set; }

    /// <summary>Invoice level discount that was applied when the basket was parked.</summary>
    public decimal InvoiceDiscount { get; set; }

    /// <summary>Invoice level discount as a percentage (0 when an amount was used).</summary>
    public decimal InvoiceDiscountPercentage { get; set; }

    public int LineCount { get; set; }

    public decimal TotalQuantity { get; set; }

    /// <summary>Estimated total at the moment of holding (display only).</summary>
    public decimal EstimatedTotal { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Lines of the parked basket.</summary>
    public List<HeldCartLineDto> Lines { get; set; } = new();
}

/// <summary>One line inside a held cart. Filled with live product data when the basket resumes.</summary>
public sealed class HeldCartLineDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal CostPrice { get; set; }

    public decimal VatRate { get; set; }

    public decimal StockOnHand { get; set; }

    public string UnitName { get; set; } = string.Empty;

    public bool IsWeighted { get; set; }

    /// <summary>False when the warehouse no longer covers the parked quantity.</summary>
    public bool IsAvailable { get; set; } = true;
}

/// <summary>Payload used by the POS to park the current basket.</summary>
public sealed class HoldCartRequest
{
    public int UserId { get; set; }

    public int? ShiftId { get; set; }

    public string TerminalId { get; set; } = "POS-01";

    public string? Label { get; set; }

    public decimal InvoiceDiscount { get; set; }

    public decimal InvoiceDiscountPercentage { get; set; }

    public decimal EstimatedTotal { get; set; }

    public IReadOnlyList<CartLineDto> Lines { get; set; } = Array.Empty<CartLineDto>();
}

/// <summary>
/// JSON payload persisted in <c>SuspendedSale.CartJson</c>. Kept as a small versioned envelope so a
/// future phase can migrate old parked baskets.
/// </summary>
public sealed class HeldCartPayload
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public decimal InvoiceDiscount { get; set; }

    public decimal InvoiceDiscountPercentage { get; set; }

    public List<HeldCartLineDto> Lines { get; set; } = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Serialize() => JsonSerializer.Serialize(this, SerializerOptions);

    public static HeldCartPayload Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new HeldCartPayload();
        }

        try
        {
            return JsonSerializer.Deserialize<HeldCartPayload>(json, SerializerOptions) ?? new HeldCartPayload();
        }
        catch (JsonException)
        {
            // A corrupted row must never stop the till: the basket simply resumes empty.
            return new HeldCartPayload();
        }
    }
}
