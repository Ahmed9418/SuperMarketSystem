using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.DependencyInjection;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Sign-in screen of the admin application. Validates the credentials against
/// <see cref="IAuthService"/> and publishes the session to <see cref="AuthSession"/>.
/// </summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserAdminService _users;
    private readonly IStoreSettingsService _settings;
    private readonly AuthSession _session;
    private readonly IDialogService _dialogs;

    public LoginViewModel(
        IAuthService authService,
        IUserAdminService users,
        IStoreSettingsService settings,
        AuthSession session,
        IDialogService dialogs)
    {
        _authService = authService;
        _users = users;
        _settings = settings;
        _session = session;
        _dialogs = dialogs;

        _ = LoadStoreNameAsync();
    }

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string storeName = "سوبر ماركت الأمانة";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>Raised when the credentials were accepted so the window can close.</summary>
    public event EventHandler? SignedIn;

    /// <summary>Executes the sign-in. The password is passed from the view because a
    /// <see cref="System.Windows.Controls.PasswordBox"/> never exposes a bindable property.</summary>
    [RelayCommand]
    private async Task SignInAsync(string? password)
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var result = await _authService.SignInAsync(UserName.Trim(), password ?? string.Empty);
            if (!result.Succeeded || result.User is null)
            {
                ErrorMessage = result.ErrorMessage ?? "تعذر تسجيل الدخول";
                return;
            }

            _session.SignIn(result.User);

            // Effective permission set (role defaults + user overrides) so the shell can hide the
            // modules this operator may not open.
            await _session.LoadPermissionsAsync(_users);

            SignedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = SqlServerDiagnostics.Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadStoreNameAsync()
    {
        try
        {
            var settings = await _settings.GetAsync();
            if (!string.IsNullOrWhiteSpace(settings.StoreName))
            {
                StoreName = settings.StoreName;
            }
        }
        catch
        {
            // The login screen must still appear when the database is unreachable; the error is
            // reported by the sign-in attempt itself.
        }
    }
}
