using System.Windows;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>
/// Standalone modal window displaying a live visual preview of the invoice
/// (80mm Thermal, A4 Standard, or A5 Compact) with the store's settings &amp; logo.
/// </summary>
public partial class InvoicePreviewWindow : Window
{
    public InvoicePreviewWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
