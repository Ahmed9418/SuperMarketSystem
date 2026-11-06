using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.DTOs;

namespace Supermarket.Admin.Views;

/// <summary>
/// Sales &amp; analytics page. The code-behind only translates UI gestures (preset change, double
/// click) into view model commands — all the logic and data lives in the view model.
/// </summary>
public partial class SalesAnalyticsView : UserControl
{
    public SalesAnalyticsView()
    {
        InitializeComponent();
    }

    private void OnPeriodChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SalesAnalyticsViewModel viewModel
            && sender is ComboBox { SelectedItem: string preset }
            && preset != viewModel.SelectedPeriod)
        {
            viewModel.SelectedPeriod = preset;
            viewModel.ApplyPresetCommand.Execute(preset);
        }
    }

    private void OnInvoiceDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SalesAnalyticsViewModel viewModel
            && InvoiceGrid.SelectedItem is InvoiceListDto row)
        {
            viewModel.OpenInvoiceCommand.Execute(row);
        }
    }
}
