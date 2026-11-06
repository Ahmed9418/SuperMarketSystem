using CommunityToolkit.Mvvm.ComponentModel;

namespace Supermarket.Admin.Services;

/// <summary>
/// Minimal page navigation for the admin shell: the main window hosts a
/// <see cref="System.Windows.Controls.ContentControl"/> whose content is the currently
/// selected view model. Keeps the view models free of any WPF dependency.
/// </summary>
public sealed class NavigationService : ObservableObject
{
    private ObservableObject? _currentViewModel;

    public ObservableObject? CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    /// <summary>Raised after a navigation so the shell can update the active menu item.</summary>
    public event EventHandler<ObservableObject>? Navigated;

    public void NavigateTo(ObservableObject viewModel)
    {
        CurrentViewModel = viewModel;
        Navigated?.Invoke(this, viewModel);
    }
}
