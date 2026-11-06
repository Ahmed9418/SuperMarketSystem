using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Supermarket.Core.Common;

namespace Supermarket.Admin.Converters;

/// <summary>
/// Formats a money value as "1,234.50 ج.م" so every financial label in the UI
/// automatically carries the Egyptian Pound suffix required by the specification.
/// </summary>
[ValueConversion(typeof(decimal), typeof(string))]
public sealed class EgpCurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            int i => i,
            long l => l,
            _ => 0m
        };

        var format = parameter as string ?? "N2";
        return $"{amount.ToString(format, CultureInfo.GetCultureInfo("ar-EG"))} {DomainConstants.CurrencySymbol}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = (value as string ?? string.Empty)
            .Replace(DomainConstants.CurrencySymbol, string.Empty)
            .Replace(",", string.Empty)
            .Trim();

        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }
}

/// <summary>
/// Colors a stock cell: red when out of stock, amber when at the alert threshold,
/// green when the level is healthy. Accepts a <c>ProductDto</c> or a raw quantity/minimum pair.
/// </summary>
public sealed class StockStatusToBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush OutOfStock = new(Color.FromRgb(0xC0, 0x39, 0x2B));
    private static readonly SolidColorBrush Low = new(Color.FromRgb(0xE6, 0x7E, 0x22));
    private static readonly SolidColorBrush Healthy = new(Color.FromRgb(0x1E, 0x84, 0x49));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return Healthy;
        }

        var quantity = ToDecimal(values[0]);
        var minimum = ToDecimal(values[1]);

        if (quantity <= 0m)
        {
            return OutOfStock;
        }

        return quantity <= minimum ? Low : Healthy;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static decimal ToDecimal(object? value) => value switch
    {
        decimal d => d,
        double dbl => (decimal)dbl,
        int i => i,
        float f => (decimal)f,
        _ => 0m
    };
}

/// <summary>Returns <see cref="Visibility.Visible"/> when the value is true.</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>Negates a boolean, used to disable a control while a command is running.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>Returns <see cref="Visibility.Visible"/> when the value is false.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not Visibility.Visible;
}

/// <summary>Collapses an element when the bound value is null.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Renders any of the domain enums with its Arabic display text
/// (see <see cref="EnumArabicExtensions"/>).
/// </summary>
public sealed class EnumArabicConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        UserRole role => role.ToArabic(),
        StockMovementType movement => movement.ToArabic(),
        PaymentMethod method => method.ToArabic(),
        ShiftStatus shift => shift.ToArabic(),
        InvoiceStatus invoice => invoice.ToArabic(),
        PurchaseOrderStatus po => po.ToArabic(),
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Hides a <see cref="FrameworkElement"/> when the bound string is empty.</summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
