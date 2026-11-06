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
    private readonly AuthSession _session;
    private readonly IDialogService _dialogs;

    public LoginViewModel(IAuthService authService, IUserAdminService users, AuthSession session, IDialogService dialogs)
    {
        _authService = authService;
        _users = users;
        _session = session;
        _dialogs = dialogs;
    }

    [ObservableProperty]
    private string userName = string.Empty;

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
            var result = await _authService.SignInAsync(UserName, password ?? string.Empty);
            if (!result.Succeeded || result.User is null)
            {
                ErrorMessage = result.ErrorMessage ?? "تعذر تسجيل الدخول";
                return;
            }

            _session.SignIn(result.User);

            // Pull the effective permission set (role defaults + the user's overrides) so the shell can
            // hide the modules this operator is not allowed to open.
            await _session.LoadPermissionsAsync(_users);

            SignedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(SqlServerDiagnostics.Describe(ex), "خطأ في تسجيل الدخول");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
