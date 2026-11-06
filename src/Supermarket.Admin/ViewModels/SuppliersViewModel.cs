using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Core.Common;
using Supermarket.Admin.Services;
using Supermarket.Admin.Views;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>Supplier management screen of the admin application.</summary>
public sealed partial class SuppliersViewModel : ObservableObject
{
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;
    private readonly AuthSession _session;

    public SuppliersViewModel(ISupplierService supplierService, IDialogService dialogs, IServiceProvider services, AuthSession session)
    {
        _supplierService = supplierService;
        _dialogs = dialogs;
        _services = services;
        _session = session;

        _ = LoadAsync();
    }

    /// <summary>Edit / delete buttons are only enabled for operators holding the edit permission.</summary>
    public bool CanEdit => _session.Can(AppPermissions.SuppliersEdit);

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    [ObservableProperty]
    private SupplierDto? selectedSupplier;

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _supplierService.GetPagedAsync(SearchTerm, 1, 200);

            Suppliers.Clear();
            foreach (var supplier in result.Items)
            {
                Suppliers.Add(supplier);
            }

            StatusMessage = $"عدد الموردين: {result.TotalCount:N0}";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل الموردين:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenEditorAsync(SupplierDto? supplier)
    {
        var dialog = new SupplierEditWindow(_services);
        await dialog.InitializeAsync(supplier?.Id ?? 0);

        if (dialog.ShowDialog() == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(SupplierDto? supplier)
    {
        supplier ??= SelectedSupplier;
        if (supplier is null)
        {
            _dialogs.ShowWarning("من فضلك اختر مورداً أولاً", "تنبيه");
            return;
        }

        try
        {
            await _supplierService.SetActiveAsync(supplier.Id, !supplier.IsActive);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, "خطأ");
        }
    }

    /// <summary>
    /// Removes the selected supplier. The confirmation names the supplier and warns that the link is
    /// detached from its products, because that is the only visible side effect of the delete.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync(SupplierDto? supplier)
    {
        supplier ??= SelectedSupplier;
        if (supplier is null)
        {
            _dialogs.ShowWarning("من فضلك اختر مورداً أولاً", "تنبيه");
            return;
        }

        if (!_session.Can(AppPermissions.SuppliersEdit))
        {
            _dialogs.ShowError("ليس لديك صلاحية حذف الموردين.", "غير مسموح");
            return;
        }

        var productNote = supplier.ProductCount > 0
            ? $"\n\nتنبيه: {supplier.ProductCount} منتج مرتبط بهذا المورد — سيتم فك الارتباط فقط ولن تُحذف المنتجات."
            : string.Empty;

        if (!_dialogs.Confirm($"هل تريد حذف المورد ({supplier.Name})؟ لا يمكن التراجع عن هذه العملية.{productNote}", "تأكيد الحذف"))
        {
            return;
        }

        try
        {
            await _supplierService.DeleteAsync(supplier.Id);
            SelectedSupplier = null;
            await LoadAsync();
            StatusMessage = $"تم حذف المورد ({supplier.Name})";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, "تعذر الحذف");
        }
    }
}
