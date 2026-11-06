using System.Windows;
using System.Windows.Input;

namespace Supermarket.POS.Views;

/// <summary>Touch friendly single line prompt used by the POS shortcuts (quantity, discount, float).</summary>
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
