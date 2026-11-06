using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Permissions matrix of one account. Every row shows the role default next to the effective value, so
/// the administrator can see at a glance what was overridden; "استعادة الافتراضي" drops all overrides.
/// </summary>
public sealed partial class PermissionsViewModel : ObservableObject
{
    private readonly IUserAdminService _users;
    private readonly IDialogService _dialogs;
    private readonly AuthSession _session;
    private UserPermissionsDto? _model;

    public PermissionsViewModel(IUserAdminService users, IDialogService dialogs, int userId, AuthSession session)
    {
        _users = users;
        _dialogs = dialogs;
        _session = session;
        UserId = userId;

        _ = LoadAsync();
    }

    public int UserId { get; }

    public ObservableCollection<PermissionRowViewModel> Rows { get; } = new();

    public IReadOnlyList<string> Groups { get; } = new[]
    {
        "عام", "المنتجات", "المخزون", "الموردون", "المبيعات", "نقطة البيع", "الإدارة"
    };

    [ObservableProperty]
    private string title = "صلاحيات المستخدم";

    [ObservableProperty]
    private string roleName = string.Empty;

    [ObservableProperty]
    private string selectedGroup = "عام";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public IEnumerable<PermissionRowViewModel> FilteredRows =>
        Rows.Where(r => SelectedGroup is null || r.Group == SelectedGroup);

    partial void OnSelectedGroupChanged(string value) => OnPropertyChanged(nameof(FilteredRows));

    /// <summary>Raised after a successful save so the window can close.</summary>
    public event EventHandler? Saved;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _model = await _users.GetPermissionsAsync(UserId);
            if (_model is null)
            {
                _dialogs.ShowWarning("المستخدم غير موجود", "تنبيه");
                return;
            }

            Title = $"صلاحيات المستخدم: {_model.FullName}";
            RoleName = _model.RoleName;

            Rows.Clear();
            foreach (var row in _model.Rows)
            {
                var vm = new PermissionRowViewModel(row);
                vm.PropertyChanged += (_, _) => OnPropertyChanged(nameof(FilteredRows));
                Rows.Add(vm);
            }

            // Default to the group that already holds something, else the first group.
            SelectedGroup = Groups.FirstOrDefault(g => Rows.Any(r => r.Group == g && r.IsGranted)) ?? Groups[0];
            OnPropertyChanged(nameof(FilteredRows));
            StatusMessage = $"{Rows.Count} صلاحية - {Rows.Count(r => r.IsGranted)} ممنوحة";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل الصلاحيات:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_model is null)
        {
            return;
        }

        var dto = new UserPermissionsDto
        {
            UserId = _model.UserId,
            FullName = _model.FullName,
            Role = _model.Role,
            Rows = Rows.Select(r => new PermissionRowDto
            {
                Key = r.Key,
                ArabicName = r.ArabicName,
                Group = r.Group,
                RoleDefault = r.RoleDefault,
                IsGranted = r.IsGranted
            }).ToList()
        };

        IsBusy = true;
        try
        {
            var result = await _users.SavePermissionsAsync(dto);
            if (!result.Succeeded)
            {
                _dialogs.ShowWarning(result.ErrorMessage ?? "تعذر حفظ الصلاحيات", "تنبيه");
                return;
            }

            // Refresh the live session when the administrator edits their own account.
            await _session.LoadPermissionsAsync(_users);

            StatusMessage = "تم حفظ الصلاحيات";
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر حفظ الصلاحيات:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GrantAll()
    {
        foreach (var row in FilteredRows)
        {
            row.IsGranted = true;
        }

        OnPropertyChanged(nameof(FilteredRows));
    }

    [RelayCommand]
    private void RevokeAll()
    {
        foreach (var row in FilteredRows)
        {
            row.IsGranted = false;
        }

        OnPropertyChanged(nameof(FilteredRows));
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        foreach (var row in Rows)
        {
            row.IsGranted = row.RoleDefault;
        }

        OnPropertyChanged(nameof(FilteredRows));
        StatusMessage = "تمت استعادة صلاحيات الدور الافتراضية";
    }

    public event EventHandler? Cancelled;

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);
}

/// <summary>One editable permission row.</summary>
public sealed partial class PermissionRowViewModel : ObservableObject
{
    public PermissionRowViewModel(PermissionRowDto dto)
    {
        Key = dto.Key;
        ArabicName = dto.ArabicName;
        Group = dto.Group;
        RoleDefault = dto.RoleDefault;
        isGranted = dto.IsGranted;
    }

    public string Key { get; }

    public string ArabicName { get; }

    public string Group { get; }

    public bool RoleDefault { get; }

    [ObservableProperty]
    private bool isGranted;

    /// <summary>"مخصص" badge when the value differs from the role default.</summary>
    public string SourceText => IsGranted == RoleDefault ? "افتراضي الدور" : "مخصص";

    public bool IsOverride => IsGranted != RoleDefault;

    partial void OnIsGrantedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOverride));
        OnPropertyChanged(nameof(SourceText));
    }
}
