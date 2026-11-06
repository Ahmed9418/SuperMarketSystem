using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Store settings screen (Phase 5): the header printed on every receipt, the printer and the fiscal
/// defaults. Saves go through <see cref="IStoreSettingsService"/>, which invalidates the cache so the
/// POS picks the new values up on its next read.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IStoreSettingsService _settings;
    private readonly IReportService _reports;
    private readonly IDialogService _dialogs;
    private readonly AuthSession _session;

    public SettingsViewModel(
        IStoreSettingsService settings,
        IReportService reports,
        IDialogService dialogs,
        AuthSession session)
    {
        _settings = settings;
        _reports = reports;
        _dialogs = dialogs;
        _session = session;

        _ = LoadAsync();
    }

    [ObservableProperty]
    private string storeName = string.Empty;

    [ObservableProperty]
    private string? address;

    [ObservableProperty]
    private string? phoneNumber;

    [ObservableProperty]
    private string? taxNumber;

    [ObservableProperty]
    private string? receiptFooterMessage;

    [ObservableProperty]
    private string? receiptPrinterName;

    [ObservableProperty]
    private int receiptLineWidth = 42;

    [ObservableProperty]
    private decimal defaultVatRate;

    [ObservableProperty]
    private bool pricesIncludeVat = true;

    [ObservableProperty]
    private int decimalPlaces = 2;

    [ObservableProperty]
    private bool autoGenerateBarcode = true;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public bool CanEdit => _session.Can(AppPermissions.SettingsEdit) || _session.IsAdministrator;

    public IReadOnlyList<int> LineWidths { get; } = new[] { 32, 42, 48, 56, 64 };

    /// <summary>Receipt width in dots, shown next to the character count.</summary>
    public string LineWidthHint => ReceiptLineWidth switch
    {
        32 => "58 مم تقريباً - 384 نقطة",
        42 => "80 مم - 576 نقطة (الأكثر شيوعاً)",
        48 => "80 مم - 576 نقطة بخط أصغر",
        _ => "تأكد من مطابقة عرض الورق للطابعة"
    };

    partial void OnReceiptLineWidthChanged(int value) => OnPropertyChanged(nameof(LineWidthHint));

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settings.GetAsync();

            StoreName = settings.StoreName;
            Address = settings.Address;
            PhoneNumber = settings.PhoneNumber;
            TaxNumber = settings.TaxNumber;
            ReceiptFooterMessage = settings.ReceiptFooterMessage;
            ReceiptPrinterName = settings.ReceiptPrinterName;
            ReceiptLineWidth = settings.ReceiptLineWidth;
            DefaultVatRate = settings.DefaultVatRate > 1m
                ? settings.DefaultVatRate
                : Math.Round(settings.DefaultVatRate * 100m, 2);
            PricesIncludeVat = settings.PricesIncludeVat;
            DecimalPlaces = settings.DecimalPlaces;
            AutoGenerateBarcode = settings.AutoGenerateBarcode;

            StatusMessage = "تم تحميل الإعدادات";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل الإعدادات:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanEdit)
        {
            _dialogs.ShowWarning("لا تمتلك صلاحية تعديل الإعدادات", "تنبيه");
            return;
        }

        if (string.IsNullOrWhiteSpace(StoreName))
        {
            _dialogs.ShowWarning("اسم المتجر مطلوب لأنه يُطبع على الإيصال", "تنبيه");
            return;
        }

        if (DefaultVatRate is < 0m or > 100m)
        {
            _dialogs.ShowWarning("نسبة الضريبة يجب أن تكون بين 0 و 100", "تنبيه");
            return;
        }

        IsBusy = true;
        try
        {
            var current = await _settings.GetAsync();

            var updated = new Core.Entities.StoreSetting
            {
                StoreName = StoreName.Trim(),
                Address = Address,
                PhoneNumber = PhoneNumber,
                TaxNumber = TaxNumber,
                ReceiptFooterMessage = ReceiptFooterMessage,
                ReceiptPrinterName = ReceiptPrinterName,
                ReceiptLineWidth = ReceiptLineWidth,
                DefaultVatRate = Math.Round(DefaultVatRate / 100m, 4),
                PricesIncludeVat = PricesIncludeVat,
                DecimalPlaces = DecimalPlaces,
                AutoGenerateBarcode = AutoGenerateBarcode
            };

            await _settings.UpdateAsync(updated);
            _ = current;

            StatusMessage = $"تم حفظ الإعدادات - {DateTime.Now:HH:mm}";
            _dialogs.ShowInformation("تم حفظ الإعدادات. ستظهر على الإيصالات والفواتير الجديدة.", "تم الحفظ");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر حفظ الإعدادات:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
