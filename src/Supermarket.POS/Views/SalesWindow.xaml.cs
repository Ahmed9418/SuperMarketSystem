using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;
using Supermarket.POS.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Supermarket.POS.Views;

/// <summary>
/// Checkout window. Owns the keyboard plumbing that cannot live in the view model:
/// continuous barcode focus, the F-key map and the "scanner typed fast" detection.
/// </summary>
public partial class SalesWindow : Window
{
    private readonly SalesViewModel _viewModel;
    private readonly IShiftService _shiftService;
    private readonly UserDto _cashier;

    public SalesWindow(SalesViewModel viewModel, IShiftService shiftService, UserDto cashier)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _shiftService = shiftService;
        _cashier = cashier;

        _viewModel.CurrentUserId = cashier.Id;
        _viewModel.ErrorSound = () => SystemSounds.Beep();
        _viewModel.FocusBarcodeRequested += (_, _) => RefocusBarcode();
        _viewModel.ShiftOpenRequested += OnShiftOpenRequested;
        _viewModel.CheckoutRequested += OnCheckoutRequested;
        _viewModel.ShiftCloseRequested += OnShiftCloseRequested;
        _viewModel.HeldCartsRequested += OnHeldCartsRequested;

        DataContext = viewModel;

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync(cashier, "POS-01");
            RefocusBarcode();
        };

        // Any click that does not land on an input control returns the keyboard to the scanner.
        PreviewMouseDown += (_, _) => Dispatcher.BeginInvoke(RefocusBarcode, DispatcherPriority.Input);
    }

    /// <summary>The barcode box must always be ready for the next scan.</summary>
    private void RefocusBarcode()
    {
        if (!BarcodeBox.IsKeyboardFocusWithin && !SearchBox.IsKeyboardFocusWithin)
        {
            BarcodeBox.Focus();
            BarcodeBox.SelectAll();
        }
    }

    private async void OnBarcodeKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                await _viewModel.ProcessBarcodeCommand.ExecuteAsync(null);
                break;

            case Key.Escape:
                e.Handled = true;
                _viewModel.BarcodeBuffer = string.Empty;
                break;
        }
    }

    /// <summary>
    /// Global shortcut map. Handled on the window so it works whatever element has the focus,
    /// except while the cashier is typing inside the search box.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        var inSearchBox = SearchBox.IsKeyboardFocusWithin;

        switch (e.Key)
        {
            case Key.F1:
                e.Handled = true;
                _viewModel.ShowHelpCommand.Execute(null);
                break;

            case Key.F2:
                e.Handled = true;
                SearchBox.Focus();
                SearchBox.SelectAll();
                break;

            case Key.F3:
                e.Handled = true;
                _viewModel.EditQuantityCommand.Execute(null);
                break;

            case Key.F4:
                e.Handled = true;
                _viewModel.EditLineDiscountCommand.Execute(null);
                break;

            case Key.F5:
                e.Handled = true;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    _viewModel.ShowHeldCartsCommand.Execute(null);
                }
                else
                {
                    _viewModel.HoldCartCommand.Execute(null);
                }

                break;

            case Key.F6:
                e.Handled = true;
                _viewModel.SelectNextLineCommand.Execute(null);
                break;

            case Key.F7:
                e.Handled = true;
                _viewModel.EditInvoiceDiscountCommand.Execute(null);
                break;

            case Key.F8:
                e.Handled = true;
                _viewModel.RemoveLineCommand.Execute(_viewModel.SelectedLine);
                break;

            case Key.F9:
                e.Handled = true;
                _viewModel.CheckoutCommand.Execute(null);
                break;

            case Key.F10:
                e.Handled = true;
                _viewModel.ShowShiftInfoCommand.Execute(null);
                break;

            case Key.Escape:
                if (!inSearchBox)
                {
                    e.Handled = true;
                    _viewModel.ClearCartCommand.Execute(null);
                }

                break;

            case Key.Add:
            case Key.OemPlus:
                if (!inSearchBox && _viewModel.SelectedLine is not null)
                {
                    _viewModel.IncreaseQuantityCommand.Execute(_viewModel.SelectedLine);
                }

                break;

            case Key.Subtract:
            case Key.OemMinus:
                if (!inSearchBox && _viewModel.SelectedLine is not null)
                {
                    _viewModel.DecreaseQuantityCommand.Execute(_viewModel.SelectedLine);
                }

                break;
        }

        if (e.Key is not (Key.F2 or Key.Escape) && !inSearchBox)
        {
            // Give the focus back to the scanner after a shortcut that opened a dialog.
            Dispatcher.BeginInvoke(RefocusBarcode, DispatcherPriority.Background);
        }
    }

    private async void OnShiftOpenRequested(object? sender, decimal openingCash)
    {
        try
        {
            var shift = await _shiftService.GetOrOpenAsync(new ShiftOpenRequestDto
            {
                UserId = _cashier.Id,
                TerminalId = "POS-01",
                OpeningCash = openingCash
            });

            _viewModel.ApplyOpenedShift(shift);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "تعذر فتح الورديّة", MessageBoxButton.OK, MessageBoxImage.Error,
                MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
    }

    /// <summary>F9: opens the payment dialog, then commits and prints the receipt.</summary>
    private async void OnCheckoutRequested(object? sender, CheckoutRequestEventArgs request)
    {
        var paymentViewModel = ActivatorUtilities.CreateInstance<PaymentViewModel>(App.Services);
        paymentViewModel.UserId = _cashier.Id;
        paymentViewModel.ShiftId = _viewModel.CurrentShiftId;

        var dialog = new PaymentWindow(paymentViewModel) { Owner = this };
        await dialog.InitializeAsync(request.Lines, request.Totals);

        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            RefocusBarcode();
            return;
        }

        var sale = dialog.Result;
        await _viewModel.HandleSaleCompletedAsync(sale);
        await PrintReceiptAsync(sale);
        RefocusBarcode();
    }

    /// <summary>Formats and prints the customer receipt; falls back to an on-screen preview.</summary>
    private async Task PrintReceiptAsync(SaleResultDto sale)
    {
        var formatter = ServiceProviderServiceExtensions.GetRequiredService<IReceiptFormatter>(App.Services);
        var settingsService = ServiceProviderServiceExtensions.GetRequiredService<IStoreSettingsService>(App.Services);
        var printer = ServiceProviderServiceExtensions.GetRequiredService<IReceiptPrinterService>(App.Services);

        var settings = await settingsService.GetAsync();
        var receiptText = formatter.BuildSaleReceipt(sale, settings);

        var print = await printer.PrintAsync(receiptText);

        if (print.Succeeded)
        {
            // Inform the cashier of the change due without blocking the next scan.
            _viewModel.StatusMessage =
                $"تمت الفاتورة {sale.InvoiceNumber} - المتبقي {sale.ChangeAmount:N2} {Core.Common.DomainConstants.CurrencySymbol} - " +
                print.Message;
            return;
        }

        // No printer: show the receipt so the cashier can read it / print it later.
        MessageBox.Show(
            $"{print.Message}\n\n{receiptText}",
            "الإيصال (معاينة)",
            MessageBoxButton.OK,
            MessageBoxImage.Information,
            MessageBoxResult.OK,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    /// <summary>Prints a small test slip so the cashier can verify paper width and Arabic shaping.</summary>
    private async void OnTestPrintClick(object sender, RoutedEventArgs e)
    {
        var printer = ServiceProviderServiceExtensions.GetRequiredService<IReceiptPrinterService>(App.Services);
        var result = await printer.PrintTestAsync();

        MessageBox.Show(result.Message, result.Succeeded ? "تمت الطباعة" : "تعذرت الطباعة",
            MessageBoxButton.OK,
            result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Warning,
            MessageBoxResult.OK,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        RefocusBarcode();
    }

    /// <summary>Shift+F5: lists the parked baskets and resumes or deletes the chosen one.</summary>
    private async void OnHeldCartsRequested(object? sender, EventArgs e)
    {
        try
        {
            var carts = await _viewModel.LoadHeldCartsAsync();
            if (carts.Count == 0)
            {
                return;
            }

            var dialog = new HeldCartsWindow(carts) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                RefocusBarcode();
                return;
            }

            if (dialog.SelectedCart is not null)
            {
                await _viewModel.ResumeHeldCartAsync(dialog.SelectedCart.Id);
            }
            else if (dialog.DeletedCart is not null)
            {
                await _viewModel.DeleteHeldCartAsync(dialog.DeletedCart.Id);
            }
        }
        finally
        {
            RefocusBarcode();
        }
    }

    /// <summary>F10: opens the shift closing screen and locks selling once the shift is closed.</summary>
    private async void OnShiftCloseRequested(object? sender, int shiftId)
    {
        var closingViewModel = ActivatorUtilities.CreateInstance<ShiftCloseViewModel>(App.Services);
        var dialog = new ShiftCloseWindow(closingViewModel) { Owner = this };

        await dialog.InitializeAsync(shiftId, isShiftClosed: false);
        dialog.ShowDialog();

        if (dialog.ShiftWasClosed)
        {
            _viewModel.ApplyClosedShift();
        }

        RefocusBarcode();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ShiftOpenRequested -= OnShiftOpenRequested;
        _viewModel.CheckoutRequested -= OnCheckoutRequested;
        _viewModel.ShiftCloseRequested -= OnShiftCloseRequested;
        base.OnClosed(e);
    }
}

/// <summary>Tiny wrapper so <see cref="SalesWindow"/> does not need the WinForms assembly.</summary>
internal static class SystemSounds
{
    public static void Beep() => Console.Beep();
}
