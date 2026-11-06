using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>
/// Sign-in window. The view model performs the authentication; the code-behind only wires
/// the <see cref="System.Windows.Controls.PasswordBox"/> with the view model (a secure
/// password box does not expose a bindable password property by design).
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private bool _opened;
    private bool _passwordVisible;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SignedIn += OnSignedIn;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UserNameBox.Focus();

    /// <summary>The password currently typed, whichever box is visible.</summary>
    private string CurrentPassword => _passwordVisible ? PasswordVisibleBox.Text : PasswordBox.Password;

    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.SignInCommand.ExecuteAsync(CurrentPassword);
    }

    private async void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await _viewModel.SignInCommand.ExecuteAsync(CurrentPassword);
        }
    }

    /// <summary>
    /// Shows / hides the password. A <see cref="PasswordBox"/> cannot reveal its own content, so the two
    /// controls are swapped and the typed value is carried over in both directions.
    /// </summary>
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
        if (_opened)
        {
            return;
        }

        _opened = true;

        var mainWindow = App.Services.GetService(typeof(MainWindow)) as MainWindow;
        if (mainWindow is null)
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
