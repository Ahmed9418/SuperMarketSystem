using System.Windows;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>Per-user permission editor: role defaults plus explicit grant / revoke overrides.</summary>
public partial class PermissionsWindow : Window
{
    private readonly PermissionsViewModel _viewModel;

    public PermissionsWindow(PermissionsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Saved += OnSaved;
        _viewModel.Cancelled += OnCancelled;
    }

    private void OnSaved(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelled(object? sender, EventArgs e) => Close();

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Saved -= OnSaved;
        _viewModel.Cancelled -= OnCancelled;
        base.OnClosed(e);
    }
}
