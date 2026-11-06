using System.Windows;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>
/// Modal editor of an account. It closes itself with <c>DialogResult = true</c> once the view model
/// reports that the account was saved, exposing the id so the list can refresh.
/// </summary>
public partial class UserEditWindow : Window
{
    private readonly UserEditViewModel _viewModel;

    public UserEditWindow(UserEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.Saved += OnSaved;
        viewModel.Cancelled += (_, _) => DialogResult = false;
    }

    /// <summary>Id of the account that was created or updated.</summary>
    public int? SavedUserId { get; private set; }

    private void OnSaved(object? sender, int userId)
    {
        SavedUserId = userId;
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
