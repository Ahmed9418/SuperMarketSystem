using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>
/// Sign-in window. The view model performs the authentication; the code-behind only wires the
/// <see cref="PasswordBox"/> (which never exposes a bindable password property) and the window drag.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private bool _signedInHandled;
    private bool _passwordVisible;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SignedIn += OnSignedIn;
        Loaded += (_, _) => UserNameBox.Focus();
    }

    /// <summary>The password currently typed, whichever box is visible.</summary>
    private string CurrentPassword => _passwordVisible ? PasswordVisibleBox.Text : PasswordBox.Password;

    /// <summary>The window has no chrome: the brand panel doubles as the title bar.</summary>
    private void OnBrandPanelMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void OnSignInClick(object sender, RoutedEventArgs e) =>
        await _viewModel.SignInCommand.ExecuteAsync(CurrentPassword);

    private async void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await _viewModel.SignInCommand.ExecuteAsync(CurrentPassword);
        }
    }

    /// <summary>Shows / hides the password by swapping the two boxes and carrying the typed value over.</summary>
    private void OnTogglePasswordClick(object sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;

        if (_passwordVisible)
        {
            PasswordVisibleBox.Text = PasswordBox.Password;
            PasswordVisibleBox.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordVisibleBox.Focus();
            PasswordVisibleBox.CaretIndex = PasswordVisibleBox.Text.Length;
        }
        else
        {
            PasswordBox.Password = PasswordVisibleBox.Text;
            PasswordBox.Visibility = Visibility.Visible;
            PasswordVisibleBox.Visibility = Visibility.Collapsed;
            PasswordBox.Focus();
        }

        TogglePasswordButton.Content = _passwordVisible ? "🙈" : "👁";
    }

    private void OnPasswordVisibleChanged(object sender, TextChangedEventArgs e)
    {
        if (_passwordVisible)
        {
            PasswordBox.Password = PasswordVisibleBox.Text;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnSignedIn(object? sender, EventArgs e)
    {
        if (_signedInHandled)
        {
            return;
        }

        _signedInHandled = true;

        if (App.Services.GetService(typeof(MainWindow)) is not MainWindow mainWindow)
        {
            Application.Current.Shutdown();
            return;
        }

        Application.Current.MainWindow = mainWindow;
        mainWindow.Show();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.SignedIn -= OnSignedIn;
        base.OnClosed(e);
    }
}
