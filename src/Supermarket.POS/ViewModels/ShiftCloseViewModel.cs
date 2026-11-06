using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.POS.Services;

namespace Supermarket.POS.ViewModels;

/// <summary>
/// Shift closing screen (تقفيل الورديّة). Builds the X report (read only) and the Z report (closing),
/// shows a live preview of both receipts and hands the formatted text to the printer.
/// </summary>
public sealed partial class ShiftCloseViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;
    private readonly IReceiptFormatter _formatter;
    private readonly IStoreSettingsService _storeSettings;
    private readonly IReceiptPrinterService _printer;
    private readonly IDialogService _dialogs;

    private StoreSetting? _settings;

    public ShiftCloseViewModel(
        IShiftService shiftService,
        IReceiptFormatter formatter,
        IStoreSettingsService storeSettings,
        IReceiptPrinterService printer,
        IDialogService dialogs)
    {
        _shiftService = shiftService;
        _formatter = formatter;
        _storeSettings = storeSettings;
        _printer = printer;
        _dialogs = dialogs;
    }

    // ------------------------------------------------------------------ State
    public int ShiftId { get; private set; }

    [ObservableProperty]
    private ShiftReportDto? report;

    [ObservableProperty]
    private decimal closingCash;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string previewText = string.Empty;

    [ObservableProperty]
    private string previewMode = "Z";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isClosed;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string printerStatus = string.Empty;

    // --------------------------------------------------------------- Derived
    public decimal ExpectedCash => Report?.ExpectedCash ?? 0m;

    public decimal Difference => Math.Round(ClosingCash - ExpectedCash, 2);

    public string DifferenceText => $"{Difference:N2} {DomainConstants.CurrencySymbol}";

    public string DifferenceVerdict => Difference switch
    {
        < 0m => "عجز في النقدية",
        > 0m => "زيادة في النقدية",
        _ => "مطابق تماماً"
    };

    public string TotalSalesText => $"{Report?.TotalSales ?? 0m:N2} {DomainConstants.CurrencySymbol}";

    public string ExpectedCashText => $"{ExpectedCash:N2} {DomainConstants.CurrencySymbol}";

    public string ShiftSummaryText => Report is null
        ? string.Empty
        : $"ورديّة {Report.ShiftNumber} - {Report.CashierName} - {Report.InvoiceCount} فاتورة";

    /// <summary>Raised after a successful close so the sales window can lock itself.</summary>
    public event EventHandler<ShiftReportDto>? ShiftClosed;

    // -------------------------------------------------------------- Lifecycle
    public async Task InitializeAsync(int shiftId, bool isShiftClosed)
    {
        ShiftId = shiftId;
        IsClosed = isShiftClosed;
        _settings = await _storeSettings.GetAsync();

        PrinterStatus = _printer.IsPrinterAvailable
            ? $"الطابعة جاهزة: {_printer.ResolvePrinterName()}"
            : "لا توجد طابعة متاحة - يمكن عرض الإيصال على الشاشة فقط";

        await RefreshAsync();
    }

    /// <summary>Rebuilds the figures (used on open and by the refresh button).</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var reportType = PreviewMode == "X" ? ShiftReportType.XReport : ShiftReportType.ZReport;
            Report = await _shiftService.BuildReportAsync(ShiftId, reportType);
            ClosingCash = Report.ClosingCash ?? Report.ExpectedCash;

            NotifyDerived();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Switches the preview between the X (peek) and Z (closing) report.</summary>
    [RelayCommand]
    private async Task ShowXReportAsync()
    {
        PreviewMode = "X";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ShowZReportAsync()
    {
        PreviewMode = "Z";
        await RefreshAsync();
    }

    partial void OnClosingCashChanged(decimal value)
    {
        OnPropertyChanged(nameof(Difference));
        OnPropertyChanged(nameof(DifferenceText));
        OnPropertyChanged(nameof(DifferenceVerdict));
        UpdatePreview();
    }

    partial void OnPreviewModeChanged(string value) => UpdatePreview();

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(ExpectedCash));
        OnPropertyChanged(nameof(ExpectedCashText));
        OnPropertyChanged(nameof(TotalSalesText));
        OnPropertyChanged(nameof(ShiftSummaryText));
        OnPropertyChanged(nameof(Difference));
        OnPropertyChanged(nameof(DifferenceText));
        OnPropertyChanged(nameof(DifferenceVerdict));
    }

    /// <summary>Rebuilds the text preview from the current numbers (no database round trip).</summary>
    private void UpdatePreview()
    {
        if (Report is null || _settings is null)
        {
            return;
        }

        // A copy keeps the preview in sync with the cashier's live input without mutating the report.
        var preview = new ShiftReportDto
        {
            ShiftId = Report.ShiftId,
            ShiftNumber = Report.ShiftNumber,
            CashierName = Report.CashierName,
            TerminalId = Report.TerminalId,
            ReportType = Report.ReportType,
            GeneratedAtUtc = Report.GeneratedAtUtc,
            OpenedAtUtc = Report.OpenedAtUtc,
            ClosedAtUtc = Report.ClosedAtUtc,
            OpeningCash = Report.OpeningCash,
            ExpectedCash = Report.ExpectedCash,
            ClosingCash = IsClosed || PreviewMode == "Z" ? ClosingCash : null,
            CashDifference = IsClosed || PreviewMode == "Z" ? Difference : 0m,
            TotalSales = Report.TotalSales,
            TotalCashSales = Report.TotalCashSales,
            TotalCardSales = Report.TotalCardSales,
            TotalWalletSales = Report.TotalWalletSales,
            TotalCreditSales = Report.TotalCreditSales,
            TotalDiscount = Report.TotalDiscount,
            TotalTax = Report.TotalTax,
            TotalRefunds = Report.TotalRefunds,
            InvoiceCount = Report.InvoiceCount,
            CancelledInvoiceCount = Report.CancelledInvoiceCount
        };

        PreviewText = _formatter.BuildShiftReport(preview, _settings);
    }

    // ---------------------------------------------------------------- Actions
    /// <summary>Prints the current preview (X while selling, Z after closing).</summary>
    [RelayCommand]
    private async Task PrintPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(PreviewText))
        {
            return;
        }

        var result = await _printer.PrintAsync(PreviewText);
        if (result.Succeeded)
        {
            _dialogs.ShowInformation(result.Message, "تمت الطباعة");
        }
        else
        {
            _dialogs.ShowWarning(result.Message, "تعذرت الطباعة");
        }
    }

    /// <summary>Closes the dialog without touching the shift (X report only).</summary>
    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Raised when the operator dismisses the window.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Closes the shift and stores the Z report.</summary>
    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (IsClosed)
        {
            return;
        }

        if (ClosingCash < 0m)
        {
            ErrorMessage = "النقدية المعدودة لا يمكن أن تكون سالبة";
            return;
        }

        var confirmation =
            $"هل تريد تقفيل الورديّة {Report?.ShiftNumber}؟\n\n" +
            $"النقدية المتوقعة: {ExpectedCashText}\n" +
            $"النقدية المعدودة: {ClosingCash:N2} {DomainConstants.CurrencySymbol}\n" +
            $"الفرق: {DifferenceText} ({DifferenceVerdict})\n\n" +
            "لن تتمكن من البيع على هذه الورديّة بعد التقفيل.";

        if (!_dialogs.Confirm(confirmation, "تقفيل الورديّة"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var closed = await _shiftService.CloseShiftAsync(new ShiftCloseRequestDto
            {
                ShiftId = ShiftId,
                ClosingCash = ClosingCash,
                Notes = Notes,
                ClosedBy = Report?.CashierName
            });

            Report = closed;
            IsClosed = true;
            PreviewMode = "Z";
            NotifyDerived();
            UpdatePreview();

            ShiftClosed?.Invoke(this, closed);

            // Auto print the Z report when a printer is available.
            var print = await _printer.PrintAsync(PreviewText);
            _dialogs.ShowInformation(
                $"تم تقفيل الورديّة بنجاح.\nالفرق: {closed.CashDifference:N2} {DomainConstants.CurrencySymbol}\n\n{print.Message}",
                "تم التقفيل");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _dialogs.ShowError(ex.Message, "تعذر تقفيل الورديّة");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
