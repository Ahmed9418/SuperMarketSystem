using System.Windows;
using Supermarket.Admin.Views;

namespace Supermarket.Admin.Services;

/// <summary>
/// Small abstraction over the WPF message boxes so the view models stay unit testable.
/// </summary>
public interface IDialogService
{
    void ShowInformation(string message, string? title = null);

    void ShowWarning(string message, string? title = null);

    void ShowError(string message, string? title = null);

    bool Confirm(string message, string? title = null);

    /// <summary>Asks for a single line of text; returns null when the user cancels.</summary>
    string? Prompt(string message, string? title = null, string? defaultValue = null);
}

/// <inheritdoc />
public sealed class DialogService : IDialogService
{
    public void ShowInformation(string message, string? title = null) =>
        MessageBox.Show(message, title ?? "معلومة", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    public void ShowWarning(string message, string? title = null) =>
        MessageBox.Show(message, title ?? "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    public void ShowError(string message, string? title = null) =>
        MessageBox.Show(message, title ?? "خطأ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    public bool Confirm(string message, string? title = null) =>
        MessageBox.Show(message, title ?? "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign) == MessageBoxResult.Yes;

    public string? Prompt(string message, string? title = null, string? defaultValue = null)
    {
        var dialog = new InputDialog(message, title ?? "إدخال", defaultValue);
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }
}
