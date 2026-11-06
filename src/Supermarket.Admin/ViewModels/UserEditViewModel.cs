using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Create / edit dialog of a user account. The password and the PIN are only required when the account
/// is new: on edit an empty box keeps the current secret, which avoids forcing a reset on every rename.
/// </summary>
public sealed partial class UserEditViewModel : ObservableObject
{
    private readonly IUserAdminService _users;
    private readonly IDialogService _dialogs;
    private readonly int _userId;

    public UserEditViewModel(IUserAdminService users, IDialogService dialogs, UserEditDto? existing)
    {
        _users = users;
        _dialogs = dialogs;
        _userId = existing?.Id ?? 0;

        IsNew = existing is null || existing.Id == 0;
        Title = IsNew ? "إضافة مستخدم جديد" : $"تعديل المستخدم: {existing!.FullName}";

        if (existing is not null)
        {
            UserName = existing.UserName;
            FullName = existing.FullName;
            SelectedRole = Roles.FirstOrDefault(r => r.Id == (int)existing.Role) ?? Roles[2];
            PhoneNumber = existing.PhoneNumber;
            IsActive = existing.IsActive;
        }
        else
        {
            SelectedRole = Roles[2];   // Cashier
            IsActive = true;
        }
    }

    public bool IsNew { get; }

    public string Title { get; }

    /// <summary>(Id, ArabicName) pairs of <see cref="UserRole"/>.</summary>
    public IReadOnlyList<LookupDto> Roles { get; } = new[]
    {
        new LookupDto { Id = (int)UserRole.Administrator, Name = UserRole.Administrator.ToArabic() },
        new LookupDto { Id = (int)UserRole.Manager, Name = UserRole.Manager.ToArabic() },
        new LookupDto { Id = (int)UserRole.Cashier, Name = UserRole.Cashier.ToArabic() },
        new LookupDto { Id = (int)UserRole.StockKeeper, Name = UserRole.StockKeeper.ToArabic() }
    };

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string fullName = string.Empty;

    [ObservableProperty]
    private LookupDto? selectedRole;

    [ObservableProperty]
    private string? phoneNumber;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string pin = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>Raised when the account was saved; the window closes and the list refreshes.</summary>
    public event EventHandler<int>? Saved;

    public string PasswordHint => IsNew
        ? "6 أحرف على الأقل"
        : "اتركها فارغة للإبقاء على كلمة المرور الحالية";

    public string PinHint => IsNew
        ? "4 أرقام (تسجيل الدخول السريع على نقطة البيع)"
        : "اتركه فارغاً للإبقاء على الرقم الحالي";

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

        var dto = new UserEditDto
        {
            Id = _userId,
            UserName = UserName?.Trim() ?? string.Empty,
            FullName = FullName?.Trim() ?? string.Empty,
            Role = (UserRole)(SelectedRole?.Id ?? (int)UserRole.Cashier),
            PhoneNumber = PhoneNumber,
            IsActive = IsActive,
            Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
            Pin = string.IsNullOrWhiteSpace(Pin) ? null : Pin
        };

        IsBusy = true;
        try
        {
            var result = IsNew ? await _users.CreateAsync(dto) : await _users.UpdateAsync(dto);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "تعذر حفظ الحساب";
                return;
            }

            Saved?.Invoke(this, result.Id ?? _userId);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _dialogs.ShowError($"تعذر حفظ الحساب:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Cancelled;
}
