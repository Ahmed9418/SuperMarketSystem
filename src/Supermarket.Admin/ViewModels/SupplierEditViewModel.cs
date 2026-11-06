using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>View model of the supplier create / edit dialog.</summary>
public sealed partial class SupplierEditViewModel : ObservableObject
{
    private readonly ISupplierService _supplierService;
    private readonly IDialogService _dialogs;

    private int _supplierId;

    public SupplierEditViewModel(ISupplierService supplierService, IDialogService dialogs)
    {
        _supplierService = supplierService;
        _dialogs = dialogs;
    }

    [ObservableProperty]
    private string supplierName = string.Empty;

    [ObservableProperty]
    private string? contactPerson;

    [ObservableProperty]
    private string? phoneNumber;

    [ObservableProperty]
    private string? email;

    [ObservableProperty]
    private string? address;

    [ObservableProperty]
    private string? taxNumber;

    [ObservableProperty]
    private decimal balance;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public string Title => _supplierId == 0 ? "إضافة مورد جديد" : "تعديل بيانات المورد";

    /// <summary>Raised after a successful save so the dialog can close itself.</summary>
    public event EventHandler? Saved;

    public async Task InitializeAsync(int supplierId)
    {
        _supplierId = supplierId;
        OnPropertyChanged(nameof(Title));

        if (supplierId <= 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var supplier = await _supplierService.GetByIdAsync(supplierId);
            if (supplier is null)
            {
                _dialogs.ShowError("لم يتم العثور على المورد", "خطأ");
                return;
            }

            SupplierName = supplier.Name;
            ContactPerson = supplier.ContactPerson;
            PhoneNumber = supplier.PhoneNumber;
            Email = supplier.Email;
            Address = supplier.Address;
            TaxNumber = supplier.TaxNumber;
            Balance = supplier.Balance;
            Notes = supplier.Notes;
            IsActive = supplier.IsActive;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(SupplierName))
        {
            ErrorMessage = "اسم المورد مطلوب";
            return;
        }

        IsBusy = true;
        try
        {
            var dto = new SupplierDto
            {
                Id = _supplierId,
                Name = SupplierName.Trim(),
                ContactPerson = ContactPerson?.Trim(),
                PhoneNumber = PhoneNumber?.Trim(),
                Email = Email?.Trim(),
                Address = Address?.Trim(),
                TaxNumber = TaxNumber?.Trim(),
                Balance = Balance,
                Notes = Notes?.Trim(),
                IsActive = IsActive
            };

            if (_supplierId > 0)
            {
                await _supplierService.UpdateAsync(dto);
            }
            else
            {
                await _supplierService.CreateAsync(dto);
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _dialogs.ShowError(ex.Message, "تعذر الحفظ");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
