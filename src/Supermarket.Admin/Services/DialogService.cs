using Supermarket.Wpf;

namespace Supermarket.Admin.Services;

/// <summary>
/// Dialog abstraction used by the view models so they stay unit-testable.
/// One implementation, Arabic and Right-to-Left on every screen.
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
