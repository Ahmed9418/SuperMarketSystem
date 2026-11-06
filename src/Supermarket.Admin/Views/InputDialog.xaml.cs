using System.Windows;

namespace Supermarket.Admin.Views;

/// <summary>
/// Single line prompt used by <c>DialogService.Prompt</c> (new customer name, price override reason, ...).
/// Kept as a window (not a WinForms input box) so it inherits the Arabic RTL theme.
/// </summary>
public partial class InputDialog : Window
{
    public InputDialog(string message, string title = "إدخال", string? defaultValue = null)
    {
        InitializeComponent();

        Title = title;
        MessageText.Text = message;
        InputBox.Text = defaultValue ?? string.Empty;

        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    /// <summary>Text the user typed; only meaningful when <c>ShowDialog()</c> returned true.</summary>
    public string InputText => InputBox.Text;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
