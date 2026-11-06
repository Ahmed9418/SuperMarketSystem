using System.Globalization;
using System.Text;

namespace Supermarket.Core.Localization;

/// <summary>
/// One place that decides how the Arabic UI renders text and numbers.
///
/// <para>
/// The UI language is Arabic (Egypt), which means the WPF windows flow Right-to-Left, while the
/// **numbers stay Latin** ("1,234.50", not "١٬٢٣٤٫٥٠") because that is what cashiers and suppliers
/// read on Egyptian price tags and invoices, and what every keyboard and barcode scanner produces.
/// </para>
///
/// <para>
/// Two problems are solved here:
/// </para>
/// <list type="number">
///   <item>Culture: <c>ar-EG</c> is selected explicitly and its number format is normalised to Latin
///   digits with "." as the decimal separator, so <c>ToString("N2")</c> is predictable everywhere.</item>
///   <item>Bidi: Arabic sentences that embed Latin text (invoice numbers, barcodes, user names) are
///   wrapped in Left-to-Right marks by <see cref="Isolate"/>, otherwise the neighbouring Arabic
///   re-orders the digits and the line reads backwards.</item>
/// </list>
/// </summary>
public static class AppCulture
{
    /// <summary>Language tag of the user interface.</summary>
    public const string LanguageTag = "ar-EG";

    /// <summary>Left-to-Right Mark: isolates an embedded Latin / numeric value.</summary>
    public const char LeftToRightMark = '\u200E';

    private static readonly object SyncRoot = new();
    private static bool _configured;

    /// <summary>The configured culture, available even before <see cref="Configure"/> runs.</summary>
    public static CultureInfo Culture { get; private set; } = CreateCulture();

    /// <summary>
    /// Installs the culture on the calling thread and,
    /// when <paramref name="forAllThreads"/> is <see langword="true"/>, on every thread created afterwards.
    /// Call it once from <c>App.OnStartup</c> before the first window is created.
    /// </summary>
    public static void Configure(bool forAllThreads = true)
    {
        lock (SyncRoot)
        {
            if (_configured)
            {
                return;
            }

            Culture = CreateCulture();

            if (forAllThreads)
            {
                CultureInfo.DefaultThreadCurrentCulture = Culture;
                CultureInfo.DefaultThreadCurrentUICulture = Culture;
            }

            CultureInfo.CurrentCulture = Culture;
            CultureInfo.CurrentUICulture = Culture;
            _configured = true;
        }
    }

    /// <summary>
    /// Wraps a dynamic value (barcode, invoice number, Latin name) in Left-to-Right marks so it stays
    /// readable when it sits inside an Arabic sentence. Empty values are returned untouched.
    /// </summary>
    public static string Isolate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Already isolated (for example by a previous call): do not stack marks.
        if (value[0] == LeftToRightMark)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 2);
        builder.Append(LeftToRightMark).Append(value).Append(LeftToRightMark);
        return builder.ToString();
    }

    private static CultureInfo CreateCulture()
    {
        // CultureInfo("UTF-8") — the value the first draft of the admin application used — throws
        // CultureNotFoundException at start-up, so the *language tag* has to be a real one.
        CultureInfo culture;

        try
        {
            culture = CultureInfo.GetCultureInfo(LanguageTag);
        }
        catch (CultureNotFoundException)
        {
            // A trimmed-down Windows image without the Arabic collation data.
            culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        }

        // Work on a copy: GetCultureInfo returns a cached read-only instance.
        culture = (CultureInfo)culture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ".";
        culture.NumberFormat.NumberGroupSeparator = ",";
        culture.NumberFormat.CurrencyDecimalSeparator = ".";
        culture.NumberFormat.CurrencyGroupSeparator = ",";
        culture.NumberFormat.DigitSubstitution = DigitShapes.None;   // Latin digits
        culture.NumberFormat.NativeDigits = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        return CultureInfo.ReadOnly(culture);
    }
}
