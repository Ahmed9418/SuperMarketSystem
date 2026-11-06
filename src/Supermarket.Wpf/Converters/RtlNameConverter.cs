using System.Globalization;
using System.Windows.Data;
using Supermarket.Core.Localization;

namespace Supermarket.Wpf.Converters;

/// <summary>
/// Marks Latin / numeric values (barcodes, invoice numbers, file paths, English user names) so that
/// they keep their own Left-to-Right reading order inside an Arabic sentence.
///
/// <para>
/// Example: <c>«الفاتورة INV-2026-0007 صادرة»</c> renders as «الفاتورة 0007-2026-INV صادرة» without the
/// isolation marks, because the numbers next to the hyphen are treated as Arabic weak characters.
/// </para>
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class RtlIsolationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        AppCulture.Isolate(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value as string ?? string.Empty;
}
