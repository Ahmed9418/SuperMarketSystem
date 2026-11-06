using System.Windows;

namespace Supermarket.Admin.Views;

/// <summary>Generic single-line text prompt used by the admin screens.</summary>
public partial class InputDialog : Window
{
    public InputDialog(string message, string title, string? defaultValue = null)
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

    /// <summary>Text entered by the operator.</summary>
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
