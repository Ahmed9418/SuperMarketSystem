using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Supermarket.Core.Common;
using Supermarket.Core.Localization;

namespace Supermarket.Wpf.Converters;

/// <summary>
/// The <c>EgpCurrency</c> converter of the XAML: <c>12.5</c> becomes <c>12.50 ج.م</c>.
/// A <see cref="string"/> parameter replaces the "N2" format (e.g. <c>N0</c> for whole pounds).
/// </summary>
[ValueConversion(typeof(decimal), typeof(string))]
public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            int i => i,
            long l => l,
            _ => 0m
        };

        return amount.ToString(parameter as string ?? "N2", AppCulture.Culture)
               + " " + DomainConstants.CurrencySymbol;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = (value as string ?? string.Empty)
            .Replace(DomainConstants.CurrencySymbol, string.Empty)
            .Replace(",", string.Empty)
            .Trim();

        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }
}

/// <summary><c>true</c> renders the element, <c>false</c> collapses it.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary><c>false</c> renders the element, <c>true</c> collapses it.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not Visibility.Visible;
}

/// <summary>Negates a boolean (a "busy" flag is inverted to drive <c>IsEnabled</c>).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;
}

/// <summary>Collapses the element when the bound value is null or an empty string.</summary>
public sealed class NotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            null => Visibility.Collapsed,
            string text => string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible,
            _ => Visibility.Visible
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Arabic caption of a domain enum (see <c>EnumArabicExtensions</c>).</summary>
public sealed class EnumArabicConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            UserRole role => role.ToArabic(),
            StockMovementType movement => movement.ToArabic(),
            PaymentMethod method => method.ToArabic(),
            ShiftStatus shift => shift.ToArabic(),
            InvoiceStatus invoice => invoice.ToArabic(),
            PurchaseOrderStatus order => order.ToArabic(),
            _ => value?.ToString() ?? string.Empty
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
