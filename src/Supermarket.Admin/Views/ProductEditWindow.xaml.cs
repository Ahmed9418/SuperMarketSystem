using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>
/// Product create / edit dialog (Phase 2). The view model is created through
/// <see cref="ActivatorUtilities"/> so the scoped Core services are injected correctly.
/// </summary>
public partial class ProductEditWindow : Window
{
    private readonly ProductEditViewModel _viewModel;

    public ProductEditWindow(IServiceProvider services)
    {
        InitializeComponent();

        _viewModel = ActivatorUtilities.CreateInstance<ProductEditViewModel>(services);
        DataContext = _viewModel;
        _viewModel.Saved += OnSaved;
    }

    /// <summary>Loads lookups and the requested product (0 creates a new product).</summary>
    public Task InitializeAsync(int productId) => _viewModel.InitializeAsync(productId);

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
