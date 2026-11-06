using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Supermarket.Core.Common;

namespace Supermarket.POS.Converters;

/// <summary>Shows an element when the bound boolean is true.</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>Shows an element when the bound boolean is false.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not Visibility.Visible;
}

/// <summary>
/// Formats money with the Egyptian Pound suffix, e.g. "27.00 ج.م".
/// The POS uses the same converter name as the admin application so the XAML stays familiar.
/// <summary>
/// Inverse boolean, kept for the numeric inputs of the closing screen: a field must stay enabled while
/// the shift is open, i.e. while <c>IsClosed</c> is false. (Binding a Visibility converter to
/// <c>IsEnabled</c> would hand a Visibility value to a bool property and silently disable the field.)
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && !flag;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && !flag;
}

/// </summary>
public sealed class EgpCurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            int i => i,
            _ => 0m
        };

        return $"{amount.ToString("N2", CultureInfo.GetCultureInfo("ar-EG"))} {DomainConstants.CurrencySymbol}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => 0m;
}
