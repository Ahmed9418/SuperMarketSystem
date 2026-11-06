using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.DependencyInjection;
using Supermarket.Core.Interfaces;

namespace Supermarket.POS.ViewModels;

/// <summary>
/// Fast cashier sign-in. The POS accepts the 4 digit PIN to keep the counter queue moving;
/// the full password path stays available for supervisors.
/// </summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserAdminService _users;
    private readonly IStoreSettingsService _settings;

    public LoginViewModel(IAuthService authService, IUserAdminService users, IStoreSettingsService settings)
    {
        _authService = authService;
        _users = users;
        _settings = settings;
    }

    /// <summary>Store name shown on the sign-in card; falls back to a neutral label.</summary>
    [ObservableProperty]
    private string storeName = "سوبر ماركت";

    /// <summary>Loads the store name; a database hiccup must not block the sign-in screen.</summary>
    public async Task LoadStoreNameAsync()
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
            // Ignore: the sign-in attempt itself reports the connectivity problem.
        }
    }

    /// <summary>Effective permission keys of the signed-in cashier (role defaults + overrides).</summary>
    public IReadOnlySet<string> Permissions { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool usePassword;

    /// <summary>Raised when the cashier was authenticated successfully.</summary>
    public event EventHandler<UserDto>? SignedIn;

    /// <summary>Switches between the quick PIN login and the full password login.</summary>
    [RelayCommand]
    private void ToggleMode()
    {
        UsePassword = !UsePassword;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SignInWithPinAsync(string? pin)
    {
        await SignInCoreAsync(() => _authService.SignInWithPinAsync(UserName, pin ?? string.Empty));
    }

    [RelayCommand]
    private async Task SignInWithPasswordAsync(string? password)
    {
        await SignInCoreAsync(() => _authService.SignInAsync(UserName, password ?? string.Empty));
    }

    private async Task SignInCoreAsync(Func<Task<AuthResult>> signIn)
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await signIn();
            if (!result.Succeeded || result.User is null)
            {
                ErrorMessage = result.ErrorMessage ?? "تعذر تسجيل الدخول";
                return;
            }

            // Permissions drive the sensitive POS actions (invoice discount, shift closing).
            try
            {
                Permissions = await _users.GetEffectivePermissionsAsync(result.User.Id);
            }
            catch
            {
                Permissions = AppPermissions.DefaultsFor(result.User.Role)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            SignedIn?.Invoke(this, result.User);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"تعذر الاتصال بقاعدة البيانات على السيرفر ({SqlServerDiagnostics.ServerName}): {ex.GetBaseException().Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
