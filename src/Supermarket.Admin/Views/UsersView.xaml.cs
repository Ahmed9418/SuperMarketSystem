using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.DTOs;

namespace Supermarket.Admin.Views;

/// <summary>
/// Users &amp; permissions page. The view owns the two modal windows (editor and permission matrix);
/// the view model only raises events and receives the result, so it stays free of WPF types.
/// </summary>
public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is UsersViewModel oldVm)
        {
            oldVm.AddUserRequested -= OnAddUserRequested;
            oldVm.EditUserRequested -= OnEditUserRequested;
            oldVm.EditPermissionsRequested -= OnEditPermissionsRequested;
        }

        if (e.NewValue is UsersViewModel newVm)
        {
            newVm.AddUserRequested += OnAddUserRequested;
            newVm.EditUserRequested += OnEditUserRequested;
            newVm.EditPermissionsRequested += OnEditPermissionsRequested;
        }
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is UsersViewModel viewModel && UsersGrid.SelectedItem is UserListItemDto)
        {
            viewModel.EditUserCommand.Execute(null);
        }
    }

    private void OnAddUserRequested(object? sender, EventArgs e) => ShowEditor(null);

    private async void OnEditUserRequested(object? sender, UserListItemDto user)
    {
        if (sender is not UsersViewModel viewModel)
        {
            return;
        }

        var dto = await viewModel.GetForEditAsync(user.Id);
        ShowEditor(dto);
    }

    private async void OnEditPermissionsRequested(object? sender, UserListItemDto user)
    {
        if (sender is not UsersViewModel viewModel)
        {
            return;
        }

        var window = new PermissionsWindow(viewModel.CreatePermissionsEditor(user.Id))
        {
            Owner = Window.GetWindow(this)
        };

        if (window.ShowDialog() == true)
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    private void ShowEditor(UserEditDto? existing)
    {
        if (DataContext is not UsersViewModel viewModel)
        {
            return;
        }

        var window = new UserEditWindow(viewModel.CreateEditor(existing))
        {
            Owner = Window.GetWindow(this)
        };

        if (window.ShowDialog() == true && window.SavedUserId is int savedId)
        {
            _ = viewModel.OnUserSavedAsync(savedId);
        }
    }
}
