using System.Windows;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Supermarket.Admin.Views;

/// <summary>
/// Supplier create / edit dialog. The view model is resolved from the DI container so that the
/// DbContext used by the form is the same scoped instance the rest of the page uses.
/// </summary>
public partial class SupplierEditWindow : Window
{
    private readonly SupplierEditViewModel _viewModel;

    public SupplierEditWindow(IServiceProvider services)
    {
        InitializeComponent();

        _viewModel = ActivatorUtilities.CreateInstance<SupplierEditViewModel>(services);
        DataContext = _viewModel;
        _viewModel.Saved += OnSaved;
    }

    /// <summary>Loads the form for the requested supplier (0 creates a new one).</summary>
    public Task InitializeAsync(int supplierId) => _viewModel.InitializeAsync(supplierId);

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
