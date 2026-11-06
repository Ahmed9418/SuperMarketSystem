using System.Windows;
using System.Windows.Input;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;
using Supermarket.POS.ViewModels;

namespace Supermarket.POS.Views;

/// <summary>
/// Cashier sign-in window. Authentication is delegated to <see cref="LoginViewModel"/>;
/// the code-behind only forwards the secure password boxes and starts the sales shell.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SignedIn += OnSignedIn;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadStoreNameAsync();
        PinBox.Focus();
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e) => await SubmitAsync();

    private async void OnPinKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SubmitAsync();
        }
    }

    private async void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SubmitAsync();
        }
    }

    private async Task SubmitAsync()
    {
        if (_viewModel.UsePassword)
        {
            await _viewModel.SignInWithPasswordCommand.ExecuteAsync(PasswordBox.Password);
        }
        else
        {
            await _viewModel.SignInWithPinCommand.ExecuteAsync(PinBox.Password);
        }

        PinBox.Clear();
        PasswordBox.Clear();
    }

    private void OnSignedIn(object? sender, UserDto user)
    {
        // Phase 3: the cashier goes straight to the checkout screen.
        var salesViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<SalesViewModel>(App.Services);

        var shiftService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IShiftService>(App.Services);

        // Phase 5: the cashier's permission set travels with the session so the till can gate the
        // sensitive actions (invoice discount, shift closing).
        salesViewModel.ApplyPermissions(_viewModel.Permissions, user.Role);

        var salesWindow = new SalesWindow(salesViewModel, shiftService, user);
        Application.Current.MainWindow = salesWindow;
        salesWindow.Show();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.SignedIn -= OnSignedIn;
        base.OnClosed(e);
    }
}
