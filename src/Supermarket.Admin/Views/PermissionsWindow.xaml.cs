using System.Windows;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>Modal permission matrix of one account.</summary>
public partial class PermissionsWindow : Window
{
    private readonly PermissionsViewModel _viewModel;

    public PermissionsWindow(PermissionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.Saved += OnSaved;
        viewModel.Cancelled += (_, _) => DialogResult = false;
    }

    private void OnSaved(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Saved -= OnSaved;
        base.OnClosed(e);
    }
}
