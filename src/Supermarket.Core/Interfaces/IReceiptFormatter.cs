using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Interfaces;

/// <summary>
/// Turns a completed sale or a shift report into the plain text that is printed on the 80 mm roll.
///
/// <para>
/// The formatter is intentionally free of any printing / OS dependency, so the exact output can be
/// unit tested and previewed on screen. The POS is responsible for rendering that text (Arabic
/// shaping + ESC/POS raster) and sending it to the printer.
/// </para>
/// </summary>
public interface IReceiptFormatter
{
    /// <summary>Customer receipt for a committed sale.</summary>
    string BuildSaleReceipt(SaleResultDto sale, StoreSetting settings);

    /// <summary>X (read only) or Z (closing) shift report.</summary>
    string BuildShiftReport(ShiftReportDto report, StoreSetting settings);

    /// <summary>Renders a single line padded to the configured width (used by the preview window).</summary>
    string PadLine(string left, string right, int width);

    /// <summary>Centers a caption on the configured width.</summary>
    string Center(string text, int width);
}

/// <summary>
/// Sends an already formatted receipt to the thermal printer.
/// Implemented by the POS application because it needs the Windows print spooler.
/// </summary>
public interface IReceiptPrinterService
{
    /// <summary>True when a printer name is configured and reachable.</summary>
    bool IsPrinterAvailable { get; }

    /// <summary>Prints the supplied plain text receipt, cutting the paper afterwards.</summary>
    Task<PrintResult> PrintAsync(string receiptText, string? printerName = null, CancellationToken cancellationToken = default);

    /// <summary>Prints a small test slip so the cashier can check alignment and paper width.</summary>
    Task<PrintResult> PrintTestAsync(string? printerName = null, CancellationToken cancellationToken = default);

    /// <summary>Configured printer name, or the Windows default printer.</summary>
    string ResolvePrinterName(string? printerName = null);
}

/// <summary>Outcome of a print attempt.</summary>
public sealed class PrintResult
{
    public bool Succeeded { get; init; }

    /// <summary>Arabic message ready to be shown to the cashier.</summary>
    public string Message { get; init; } = string.Empty;

    public string? PrinterName { get; init; }

    public static PrintResult Ok(string printerName) => new()
    {
        Succeeded = true,
        PrinterName = printerName,
        Message = $"تم إرسال الإيصال إلى الطابعة ({printerName})"
    };

    public static PrintResult Fail(string message, string? printerName = null) => new()
    {
        Succeeded = false,
        PrinterName = printerName,
        Message = message
    };
}
