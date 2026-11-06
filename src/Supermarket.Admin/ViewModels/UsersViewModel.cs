using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.Admin.Services;
using Supermarket.Admin.Views;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Users &amp; permissions screen (Phase 5). Lists every account with its role, its activity state and
/// the number of permissions it actually holds, and exposes the administration actions.
///
/// <para>
/// The dialogs are opened by the code-behind of the view (a view model never creates a window); every
/// action is therefore a command that the view translates into a modal dialog, and the result is fed
/// back through the <c>Apply…</c> methods below.
/// </para>
/// </summary>
public sealed partial class UsersViewModel : ObservableObject
{
    private readonly IUserAdminService _users;
    private readonly IServiceProvider _services;
    private readonly IDialogService _dialogs;
    private readonly AuthSession _session;

    public UsersViewModel(IUserAdminService users, IServiceProvider services, IDialogService dialogs, AuthSession session)
    {
        _users = users;
        _services = services;
        _dialogs = dialogs;
        _session = session;

        _ = LoadAsync();
    }

    public ObservableCollection<UserListItemDto> Users { get; } = new();

    public IReadOnlyList<string> RoleFilters { get; } = new[]
    {
        "كل الأدوار", "مدير النظام", "مشرف", "كاشير", "أمين مخزن"
    };

    [ObservableProperty]
    private UserListItemDto? selectedUser;

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private string roleFilter = "كل الأدوار";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public bool CanManage => _session.Can(AppPermissions.UsersManage);

    public string CurrentUserHint => $"الحساب الحالي: {_session.DisplayName}";

    /// <summary>The rows after the search + role filter (filtering is client side: the list is small).</summary>
    public IEnumerable<UserListItemDto> FilteredUsers
    {
        get
        {
            var term = (SearchTerm ?? string.Empty).Trim();

            return Users.Where(u =>
            {
                var matchesRole = RoleFilter switch
                {
                    "مدير النظام" => u.Role == UserRole.Administrator,
                    "مشرف" => u.Role == UserRole.Manager,
                    "كاشير" => u.Role == UserRole.Cashier,
                    "أمين مخزن" => u.Role == UserRole.StockKeeper,
                    _ => true
                };

                var matchesTerm = term.Length == 0
                                  || u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                                  || u.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)
                                  || (u.PhoneNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

                return matchesRole && matchesTerm;
            });
        }
    }

    partial void OnSearchTermChanged(string value) => OnPropertyChanged(nameof(FilteredUsers));

    partial void OnRoleFilterChanged(string value) => OnPropertyChanged(nameof(FilteredUsers));

    // ------------------------------------------------------------------ View hand-offs
    /// <summary>Raised by the toolbar: create a new account.</summary>
    public event EventHandler? AddUserRequested;

    /// <summary>Raised by the toolbar: edit the selected account.</summary>
    public event EventHandler<UserListItemDto>? EditUserRequested;

    /// <summary>Raised by the toolbar: open the permission matrix of the selected account.</summary>
    public event EventHandler<UserListItemDto>? EditPermissionsRequested;

    // ------------------------------------------------------------------ Commands
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var rows = await _users.GetAllAsync();
            Users.Clear();
            foreach (var row in rows)
            {
                Users.Add(row);
            }

            OnPropertyChanged(nameof(FilteredUsers));
            StatusMessage = $"{Users.Count:N0} حساب - {Users.Count(u => u.IsActive):N0} نشط";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل المستخدمين:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Loads the editable projection of an account for the editor window.</summary>
    public async Task<UserEditDto?> GetForEditAsync(int userId) => await _users.GetForEditAsync(userId);

    /// <summary>Creates the view model of the editor window (the view resolves it from DI).</summary>
    public UserEditViewModel CreateEditor(UserEditDto? existing) =>
        new(_users, _dialogs, existing);

    /// <summary>Creates the view model of the permissions window.</summary>
    public PermissionsViewModel CreatePermissionsEditor(int userId) =>
        new(_users, _dialogs, userId, _session);

    [RelayCommand]
    private void AddUser()
    {
        if (!CanManage)
        {
            _dialogs.ShowWarning("لا تمتلك صلاحية إدارة المستخدمين", "تنبيه");
            return;
        }

        AddUserRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void EditUser(UserListItemDto? user)
    {
        user ??= SelectedUser;
        if (user is null)
        {
            _dialogs.ShowWarning("اختر مستخدماً من القائمة أولاً", "تنبيه");
            return;
        }

        if (!CanManage)
        {
            _dialogs.ShowWarning("لا تمتلك صلاحية إدارة المستخدمين", "تنبيه");
            return;
        }

        EditUserRequested?.Invoke(this, user);
    }

    [RelayCommand]
    private void EditPermissions(UserListItemDto? user)
    {
        user ??= SelectedUser;
        if (user is null)
        {
            _dialogs.ShowWarning("اختر مستخدماً من القائمة أولاً", "تنبيه");
            return;
        }

        if (!CanManage)
        {
            _dialogs.ShowWarning("لا تمتلك صلاحية إدارة المستخدمين", "تنبيه");
            return;
        }

        EditPermissionsRequested?.Invoke(this, user);
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(UserListItemDto? user)
    {
        user ??= SelectedUser;
        if (user is null)
        {
            return;
        }

        if (!CanManage)
        {
            _dialogs.ShowWarning("لا تمتلك صلاحية إدارة المستخدمين", "تنبيه");
            return;
        }

        var action = user.IsActive ? "إيقاف" : "تنشيط";
        if (!_dialogs.Confirm($"هل تريد {action} حساب ({user.FullName})؟", "تأكيد"))
        {
            return;
        }

        var result = await _users.SetActiveAsync(user.Id, !user.IsActive);
        if (!result.Succeeded)
        {
            _dialogs.ShowWarning(result.ErrorMessage ?? "تعذر تنفيذ العملية", "تنبيه");
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(UserListItemDto? user)
    {
        user ??= SelectedUser;
        if (user is null || !CanManage)
        {
            return;
        }

        var input = _dialogs.Prompt($"كلمة المرور الجديدة للمستخدم ({user.FullName}):", "إعادة تعيين كلمة المرور");
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var result = await _users.ResetPasswordAsync(user.Id, input);
        if (result.Succeeded)
        {
            _dialogs.ShowInformation("تم تغيير كلمة المرور بنجاح", "تم");
        }
        else
        {
            _dialogs.ShowWarning(result.ErrorMessage ?? "تعذر تغيير كلمة المرور", "تنبيه");
        }
    }

    [RelayCommand]
    private async Task ResetPinAsync(UserListItemDto? user)
    {
        user ??= SelectedUser;
        if (user is null || !CanManage)
        {
            return;
        }

        var input = _dialogs.Prompt($"الرقم السري الجديد (4 أرقام) للمستخدم ({user.FullName}):", "إعادة تعيين الرقم السري");
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var result = await _users.ResetPinAsync(user.Id, input);
        if (result.Succeeded)
        {
            _dialogs.ShowInformation("تم تغيير الرقم السري بنجاح", "تم");
        }
        else
        {
            _dialogs.ShowWarning(result.ErrorMessage ?? "تعذر تغيير الرقم السري", "تنبيه");
        }
    }

    /// <summary>Called by the view after the editor window closed with a saved user.</summary>
    public async Task OnUserSavedAsync(int userId)
    {
        await LoadAsync();
        StatusMessage = $"تم حفظ بيانات الحساب رقم {userId}";
    }
}
