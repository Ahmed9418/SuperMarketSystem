using Supermarket.Wpf;

namespace Supermarket.POS.Services;

/// <summary>
/// Message boxes for the POS, always right-aligned because the cashier reads Arabic.
/// Kept behind an interface so the view models stay unit-testable.
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
    public void ShowInformation(string message, string? title = null) =>
        ArabicMessageBox.Show(message, title ?? "معلومة");

    public void ShowWarning(string message, string? title = null) =>
        ArabicMessageBox.ShowWarning(message, title ?? "تنبيه");

    public void ShowError(string message, string? title = null) =>
        ArabicMessageBox.ShowError(message, title ?? "خطأ");

    public bool Confirm(string message, string? title = null) =>
        ArabicMessageBox.Confirm(message, title ?? "تأكيد");

    public string? Prompt(string message, string? title = null, string? defaultValue = null)
    {
        var dialog = new Views.InputDialog(message, title ?? "إدخال", defaultValue);
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }
}
