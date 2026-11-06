using System.Windows;
using Supermarket.POS.Views;

namespace Supermarket.POS.Services;

/// <summary>
/// Message boxes for the POS, always right aligned because the cashier reads Arabic.
/// Kept behind an interface so the view models stay unit testable.
/// </summary>
public interface IDialogService
{
    void ShowInformation(string message, string? title = null);

    void ShowWarning(string message, string? title = null);

    void ShowError(string message, string? title = null);

    bool Confirm(string message, string? title = null);

    /// <summary>Single line text prompt; returns null when the cashier cancels.</summary>
    string? Prompt(string message, string? title = null, string? defaultValue = null);
}

/// <inheritdoc />
public sealed class DialogService : IDialogService
{
    private const MessageBoxOptions Rtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    public void ShowInformation(string message, string? title = null) =>
        MessageBox.Show(message, title ?? "معلومة", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, Rtl);

    public void ShowWarning(string message, string? title = null) =>
        MessageBox.Show(message, title ?? "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, Rtl);

    public void ShowError(string message, string? title = null) =>
        MessageBox.Show(message, title ?? "خطأ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, Rtl);

    public bool Confirm(string message, string? title = null) =>
        MessageBox.Show(message, title ?? "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, Rtl)
        == MessageBoxResult.Yes;

    public string? Prompt(string message, string? title = null, string? defaultValue = null)
    {
        var dialog = new InputDialog(message, title ?? "إدخال", defaultValue);
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }
}
