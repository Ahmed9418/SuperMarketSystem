using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Wpf.Printing;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Store settings screen: store identity, invoice paper size (80mm, A4, A5),
/// store logo, thermal printer, tax defaults, and test print.
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

        LoadInstalledPrinters();
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
    private string invoicePaperSize = "80mm";

    [ObservableProperty]
    private string? logoPath;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoPath) && File.Exists(LogoPath);

    public bool CanEdit => _session.Can(AppPermissions.SettingsEdit) || _session.IsAdministrator;

    public IReadOnlyList<int> LineWidths { get; } = new[] { 32, 42, 48, 56, 64 };

    public ObservableCollection<string> InstalledPrinters { get; } = new();

    public IReadOnlyList<PaperSizeOption> PaperSizes { get; } = new[]
    {
        new PaperSizeOption("80mm", "إيصال حراري (80 مم - المعتاد للكاشير)", "طباعة رول حراري سريعة 576 نقطة"),
        new PaperSizeOption("A4", "فاتورة ضريبية قياسية (A4 - صفحة كاملة)", "طباعة ليزر / عادية مع جداول مفصلة"),
        new PaperSizeOption("A5", "فاتورة مدمجة (A5 - نصف صفحة)", "حجم اقتصادي مناسب للشركات والموردين")
    };

    public PaperSizeOption? SelectedPaperSizeOption
    {
        get => PaperSizes.FirstOrDefault(p => string.Equals(p.Key, InvoicePaperSize, StringComparison.OrdinalIgnoreCase)) ?? PaperSizes[0];
        set
        {
            if (value != null && InvoicePaperSize != value.Key)
            {
                InvoicePaperSize = value.Key;
                OnPropertyChanged(nameof(InvoicePaperSize));
                OnPropertyChanged(nameof(SelectedPaperSizeOption));
                OnPropertyChanged(nameof(IsThermalSelected));
            }
        }
    }

    public bool IsThermalSelected => string.Equals(InvoicePaperSize, "80mm", StringComparison.OrdinalIgnoreCase);

    /// <summary>Receipt width in dots, shown next to the character count.</summary>
    public string LineWidthHint => ReceiptLineWidth switch
    {
        32 => "58 مم تقريباً - 384 نقطة",
        42 => "80 مم - 576 نقطة (الأكثر شيوعاً)",
        48 => "80 مم - 576 نقطة بخط أصغر",
        _ => "تأكد من مطابقة عرض الورق للطابعة"
    };

    partial void OnReceiptLineWidthChanged(int value) => OnPropertyChanged(nameof(LineWidthHint));

    partial void OnInvoicePaperSizeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedPaperSizeOption));
        OnPropertyChanged(nameof(IsThermalSelected));
    }

    partial void OnLogoPathChanged(string? value) => OnPropertyChanged(nameof(HasLogo));

    private void LoadInstalledPrinters()
    {
        InstalledPrinters.Clear();
        foreach (var printer in RawPrinterHelper.GetInstalledPrinterNames())
        {
            InstalledPrinters.Add(printer);
        }
    }

    [RelayCommand]
    private void ChooseLogo()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "اختر شعار المتجر (لوجو)",
                Filter = "ملفات الصور (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|جميع الملفات (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SupermarketSystem", "Logos");
                Directory.CreateDirectory(dir);

                var ext = Path.GetExtension(dialog.FileName);
                var dest = Path.Combine(dir, $"store_logo_{DateTime.Now:yyyyMMddHHmmss}{ext}");
                File.Copy(dialog.FileName, dest, overwrite: true);

                LogoPath = dest;
                StatusMessage = "تم اختيار الشعار. اضغط حفظ الإعدادات لتثبيته.";
            }
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر اختيار الشعار:\n{ex.Message}", "خطأ");
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        LogoPath = null;
        StatusMessage = "تمت إزالة الشعار. اضغط حفظ الإعدادات لتثبيت التغيير.";
    }

    [RelayCommand]
    private async Task TestPrintAsync()
    {
        IsBusy = true;
        StatusMessage = "جارٍ اختبار الطباعة...";

        try
        {
            var testSetting = new StoreSetting
            {
                StoreName = string.IsNullOrWhiteSpace(StoreName) ? "سوبر ماركت" : StoreName.Trim(),
                Address = Address,
                PhoneNumber = PhoneNumber,
                TaxNumber = TaxNumber,
                ReceiptFooterMessage = ReceiptFooterMessage,
                ReceiptPrinterName = ReceiptPrinterName,
                ReceiptLineWidth = ReceiptLineWidth,
                InvoicePaperSize = InvoicePaperSize,
                LogoPath = LogoPath,
                DefaultVatRate = Math.Round(DefaultVatRate / 100m, 4),
                PricesIncludeVat = PricesIncludeVat,
                DecimalPlaces = DecimalPlaces
            };

            var result = await InvoicePrintHelper.PrintTestAsync(testSetting, ReceiptPrinterName);
            if (result.Succeeded)
            {
                StatusMessage = $"تمت الطباعة بنجاح على: {result.PrinterName}";
                _dialogs.ShowInformation($"تم إرسال أمر الطباعة بنجاح إلى:\n{result.PrinterName}", "نجاح الطباعة");
            }
            else
            {
                StatusMessage = "تعذرت الطباعة";
                _dialogs.ShowWarning(result.Message, "تنبيه الطباعة");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "خطأ في أمر الطباعة";
            _dialogs.ShowError($"تعذر تنفيذ اختبار الطباعة:\n{ex.Message}", "خطأ في الطباعة");
        }
        finally
        {
            IsBusy = false;
        }
    }

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
            InvoicePaperSize = string.IsNullOrWhiteSpace(settings.InvoicePaperSize) ? "80mm" : settings.InvoicePaperSize;
            LogoPath = settings.LogoPath;

            OnPropertyChanged(nameof(SelectedPaperSizeOption));
            OnPropertyChanged(nameof(IsThermalSelected));
            OnPropertyChanged(nameof(HasLogo));

            LoadInstalledPrinters();
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
            var updated = new StoreSetting
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
                AutoGenerateBarcode = AutoGenerateBarcode,
                InvoicePaperSize = InvoicePaperSize,
                LogoPath = LogoPath
            };

            await _settings.UpdateAsync(updated);

            StatusMessage = $"تم حفظ الإعدادات - {DateTime.Now:HH:mm}";
            _dialogs.ShowInformation("تم حفظ الإعدادات بنجاح. ستظهر التغييرات على الفواتير والإيصالات الجديدة.", "تم الحفظ");
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

public sealed class PaperSizeOption
{
    public PaperSizeOption(string key, string title, string description)
    {
        Key = key;
        Title = title;
        Description = description;
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }

    public override string ToString() => Title;
}
