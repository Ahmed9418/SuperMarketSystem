using Microsoft.Extensions.Configuration;
using Supermarket.Core.Interfaces;

namespace Supermarket.POS.Printing;

/// <summary>
/// Sends receipts to the 80 mm thermal printer. The name is resolved in this order:
/// <c>appsettings.json</c> (<c>Pos:PrinterName</c>) → <c>StoreSetting.ReceiptPrinterName</c> (the value
/// maintained on the admin settings screen) → the Windows default printer.
///
/// <para>
/// The text receipt produced by <see cref="IReceiptFormatter"/> is typeset with WPF (correct Arabic
/// shaping and bidi), rendered to a 576-dot bitmap and pushed to the spooler as an ESC/POS
/// <c>GS v 0</c> raster image, followed by a feed and a paper cut.
/// </para>
/// </summary>
public sealed class ThermalPrinterService : IReceiptPrinterService
{
    /// <summary>ESC @ (reset) + ESC a 1 (centre) prefix.</summary>
    private static readonly byte[] InitCommands = { 0x1B, 0x40 };

    /// <summary>Feed 4 lines + GS V 1 (partial cut).</summary>
    private static readonly byte[] CutCommands = { 0x1B, 0x64, 0x04, 0x1D, 0x56, 0x01 };

    private readonly string? _configuredPrinter;
    private readonly double _fontSize;
    private readonly IStoreSettingsService _storeSettings;

    public ThermalPrinterService(IConfiguration configuration, IStoreSettingsService storeSettings)
    {
        _configuredPrinter = configuration["Pos:PrinterName"];
        _fontSize = double.TryParse(configuration["Pos:ReceiptFontSize"], out var size) ? size : 20d;
        _storeSettings = storeSettings;
    }

    /// <inheritdoc />
    public bool IsPrinterAvailable
    {
        get
        {
            var name = ResolvePrinterName();
            return !string.IsNullOrWhiteSpace(name) && RawPrinterHelper.PrinterExists(name);
        }
    }

    /// <inheritdoc />
    public string ResolvePrinterName(string? printerName = null)
    {
        if (!string.IsNullOrWhiteSpace(printerName))
        {
            return printerName;
        }

        if (!string.IsNullOrWhiteSpace(_configuredPrinter))
        {
            return _configuredPrinter;
        }

        return RawPrinterHelper.GetDefaultPrinterName() ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<PrintResult> PrintAsync(string receiptText, string? printerName = null, CancellationToken cancellationToken = default)
    {
        // The admin settings screen is the friendly place to type the printer name, so it is honoured
        // when appsettings.json leaves Pos:PrinterName empty.
        var name = ResolvePrinterName(printerName);
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, _configuredPrinter, StringComparison.OrdinalIgnoreCase))
        {
            name = await ResolveFromStoreSettingsAsync(name, cancellationToken);
        }

        return await Task.Run(() =>
        {

            if (string.IsNullOrWhiteSpace(name))
            {
                return PrintResult.Fail("لم يتم العثور على طابعة إيصالات. برجاء ضبط اسم الطابعة في الإعدادات.");
            }

            if (string.IsNullOrWhiteSpace(receiptText))
            {
                return PrintResult.Fail("لا يوجد محتوى للطباعة.");
            }

            try
            {
                var bitmap = EscPosImageBuilder.RenderText(receiptText, fontSize: _fontSize);
                var raster = EscPosImageBuilder.BuildRasterCommands(bitmap);

                var payload = new byte[InitCommands.Length + raster.Length + CutCommands.Length];
                Buffer.BlockCopy(InitCommands, 0, payload, 0, InitCommands.Length);
                Buffer.BlockCopy(raster, 0, payload, InitCommands.Length, raster.Length);
                Buffer.BlockCopy(CutCommands, 0, payload, InitCommands.Length + raster.Length, CutCommands.Length);

                RawPrinterHelper.SendBytes(name, payload, $"Receipt-{DateTime.Now:yyyyMMdd-HHmmss}");

                return PrintResult.Ok(name);
            }
            catch (Exception ex)
            {
                return PrintResult.Fail($"تعذرت الطباعة: {ex.Message}", name);
            }
        }, cancellationToken);
    }

    /// <summary>Reads the printer name maintained on the admin settings screen (cached, never throws).</summary>
    private async Task<string> ResolveFromStoreSettingsAsync(string? current, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _storeSettings.GetAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(settings.ReceiptPrinterName))
            {
                return settings.ReceiptPrinterName!;
            }
        }
        catch
        {
            // A settings failure must never block the till: fall back to what we already had.
        }

        return current ?? string.Empty;
    }

    /// <inheritdoc />
    public Task<PrintResult> PrintTestAsync(string? printerName = null, CancellationToken cancellationToken = default)
    {
        const string test =
            "اختبار طباعة الإيصال\n" +
            "=============================\n" +
            "سوبر ماركت الأمانة\n" +
            "أخميم - سوهاج\n" +
            "ت: 01001234567\n" +
            "=============================\n" +
            "صنف تجريبي        3 × 27.00\n" +
            "الإجمالي        81.00 ج.م\n" +
            "=============================\n" +
            "إذا ظهرت الحروف العربية سليمة\n" +
            "فهذا يعني أن الطابعة مضبوطة\n";

        return PrintAsync(test, printerName, cancellationToken);
    }
}
