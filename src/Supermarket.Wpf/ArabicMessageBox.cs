using System.Windows;

namespace Supermarket.Wpf;

/// <summary>
/// Right-to-left message boxes. <c>MessageBoxOptions.RtlReading</c> only has an effect when
/// <c>RightAlign</c> is passed with it, which is exactly the mistake that made the Arabic dialogs of
/// the first draft read Left-to-Right. Every call now goes through this one helper.
/// </summary>
public static class ArabicMessageBox
{
    private const MessageBoxOptions Rtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    public static void Show(string message, string title = "معلومة") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, Rtl);

    public static void ShowWarning(string message, string title = "تنبيه") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, Rtl);

    public static void ShowError(string message, string title = "خطأ") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, Rtl);

    /// <summary>True when the operator answered "Yes". "No" is the default button.</summary>
    public static bool Confirm(string message, string title = "تأكيد") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, Rtl)
        == MessageBoxResult.Yes;

    /// <summary>Yes / No / Cancel, used when an action may be abandoned halfway.</summary>
    public static MessageBoxResult Ask(string message, string title = "تأكيد") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question,
            MessageBoxResult.Cancel, Rtl);
}
