using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>Manual stock correction dialog (تسوية مخزون).</summary>
public partial class StockAdjustmentWindow : Window
{
    private readonly StockAdjustmentViewModel _viewModel;

    public StockAdjustmentWindow(IServiceProvider services)
    {
        InitializeComponent();

        _viewModel = ActivatorUtilities.CreateInstance<StockAdjustmentViewModel>(services);
        DataContext = _viewModel;
        _viewModel.Saved += OnSaved;
    }

    /// <summary>Optionally preselects the product the user right-clicked.</summary>
    public Task InitializeAsync(int? productId) => _viewModel.InitializeAsync(productId);

    private void OnSaved(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Saved -= OnSaved;
        base.OnClosed(e);
    }
}
