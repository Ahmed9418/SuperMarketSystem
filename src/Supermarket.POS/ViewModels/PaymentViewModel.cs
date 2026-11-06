using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Localization;
using Supermarket.POS.Services;

namespace Supermarket.POS.ViewModels;

/// <summary>
/// Payment dialog (Phase 4). Supports cash / card / wallet / credit, quick cash denominations,
/// automatic change calculation and an optional customer for credit sales or loyalty capture.
/// </summary>
public sealed partial class PaymentViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly ISalesService _salesService;
    private readonly IDialogService _dialogs;

    public PaymentViewModel(ICatalogService catalogService, ISalesService salesService, IDialogService dialogs)
    {
        _catalogService = catalogService;
        _salesService = salesService;
        _dialogs = dialogs;
    }

    // ------------------------------------------------------------------ Input
    /// <summary>Cart to be paid; set by the sales screen before opening the dialog.</summary>
    public IReadOnlyList<CartLineDto> Lines { get; private set; } = Array.Empty<CartLineDto>();

    public int UserId { get; set; }

    public int? ShiftId { get; set; }

    public CartPricingDto Totals { get; private set; } = new();

    [ObservableProperty]
    private PaymentMethodOption? selectedMethod;

    [ObservableProperty]
    private decimal paidAmount;

    [ObservableProperty]
    private ObservableCollection<LookupDto> customers = new();

    [ObservableProperty]
    private LookupDto? selectedCustomer;

    [ObservableProperty]
    private string? newCustomerName;

    [ObservableProperty]
    private string? newCustomerPhone;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    // --------------------------------------------------------------- Derived
    public IReadOnlyList<PaymentMethodOption> Methods { get; } = new[]
    {
        new PaymentMethodOption(PaymentMethod.Cash, "نقدي"),
        new PaymentMethodOption(PaymentMethod.Card, "بطاقة"),
        new PaymentMethodOption(PaymentMethod.Wallet, "محفظة إلكترونية"),
        new PaymentMethodOption(PaymentMethod.Credit, "آجل")
    };

    /// <summary>Suggested notes / coins shown as big touch buttons.</summary>
    public IReadOnlyList<QuickCashOption> QuickCashOptions { get; private set; } = Array.Empty<QuickCashOption>();

    public string TotalText => $"{Totals.TotalAmount:N2} {DomainConstants.CurrencySymbol}";

    public string PaidText => $"{PaidAmount:N2} {DomainConstants.CurrencySymbol}";

    public string ChangeText => $"{ChangeAmount:N2} {DomainConstants.CurrencySymbol}";

    public string NetText => $"{Totals.NetTotal:N2} {DomainConstants.CurrencySymbol}";

    public string TaxText => $"{Totals.TaxAmount:N2} {DomainConstants.CurrencySymbol}";

    public string DiscountText => $"{Totals.TotalDiscount:N2} {DomainConstants.CurrencySymbol}";

    public decimal ChangeAmount => SelectedMethod?.Method == PaymentMethod.Credit
        ? 0m
        : Math.Max(0m, Math.Round(PaidAmount - Totals.TotalAmount, 2));

    public decimal RemainingAmount => SelectedMethod?.Method == PaymentMethod.Credit
        ? Totals.TotalAmount
        : Math.Max(0m, Math.Round(Totals.TotalAmount - PaidAmount, 2));

    public bool IsCredit => SelectedMethod?.Method == PaymentMethod.Credit;

    public bool IsCash => SelectedMethod?.Method == PaymentMethod.Cash;

    public bool CanConfirm => IsCredit || PaidAmount >= Totals.TotalAmount;

    /// <summary>Confirmed sale, consumed by the sales window for printing.</summary>
    public SaleResultDto? Result { get; private set; }

    /// <summary>Raised when the payment was committed successfully.</summary>
    public event EventHandler<SaleResultDto>? Completed;

    // -------------------------------------------------------------- Lifecycle
    public async Task InitializeAsync(IReadOnlyList<CartLineDto> lines, CartPricingDto totals)
    {
        Lines = lines;
        Totals = totals;
        SelectedMethod = Methods[0];
        PaidAmount = totals.TotalAmount;
        BuildQuickCashOptions();
        NotifyTotals();

        try
        {
            Customers.Clear();
            foreach (var customer in await _catalogService.GetCustomersAsync())
            {
                Customers.Add(customer);
            }
        }
        catch
        {
            // A missing customer list must never block a cash sale.
        }
    }

    /// <summary>Rounds the total up to the next 5 / 10 / 50 / 100 / 200 for one-touch cash entry.</summary>
    private void BuildQuickCashOptions()
    {
        var total = Totals.TotalAmount;
        var candidates = new[] { 5m, 10m, 20m, 50m, 100m, 200m, 500m };

        var options = new List<QuickCashOption>();

        foreach (var step in candidates)
        {
            var rounded = Math.Ceiling(total / step) * step;
            if (rounded > 0 && options.All(o => o.Amount != rounded))
            {
                options.Add(new QuickCashOption(rounded, $"{rounded:N0}"));
            }
        }

        // Exact amount first, then the rounded notes.
        QuickCashOptions = new[] { new QuickCashOption(total, "المبلغ بالضبط") }
            .Concat(options.Where(o => o.Amount != total).Take(5))
            .ToList();

        OnPropertyChanged(nameof(QuickCashOptions));
    }

    [RelayCommand]
    private void SelectMethod(PaymentMethodOption? option)
    {
        if (option is null)
        {
            return;
        }

        SelectedMethod = option;

        if (option.Method == PaymentMethod.Credit)
        {
            PaidAmount = 0m;
        }
        else if (PaidAmount <= 0m)
        {
            PaidAmount = Totals.TotalAmount;
        }

        NotifyTotals();
    }

    [RelayCommand]
    private void SetPaidAmount(QuickCashOption? option)
    {
        if (option is null)
        {
            return;
        }

        PaidAmount = option.Amount;
    }

    [RelayCommand]
    private void AppendDigit(string? digit)
    {
        if (string.IsNullOrEmpty(digit) || !char.IsDigit(digit[0]))
        {
            return;
        }

        var current = PaidAmount.ToString("0.##");
        var candidate = current == "0" ? digit : current + digit;

        if (decimal.TryParse(candidate, out var value) && value <= 999_999m)
        {
            PaidAmount = value;
        }
    }

    [RelayCommand]
    private void ClearPaid() => PaidAmount = 0m;

    /// <summary>Exact amount button: pays the grand total to the piastre.</summary>
    [RelayCommand]
    private void SetExactPaid() => PaidAmount = Totals.TotalAmount;

    [RelayCommand]
    private void AddDecimal() => PaidAmount = Math.Round(PaidAmount + 0.5m, 2);

    partial void OnPaidAmountChanged(decimal value) => NotifyTotals();

    partial void OnSelectedMethodChanged(PaymentMethodOption? value)
    {
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(IsCash));
        NotifyTotals();
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(PaidText));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(NetText));
        OnPropertyChanged(nameof(TaxText));
        OnPropertyChanged(nameof(DiscountText));
        OnPropertyChanged(nameof(ChangeAmount));
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(CanConfirm));
    }

    // ---------------------------------------------------------------- Confirm
    /// <summary>Commits the sale through <see cref="ISalesService"/>.</summary>
    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = Validate();
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var customerId = SelectedCustomer?.Id;

            var request = new SaleRequestDto
            {
                UserId = UserId,
                ShiftId = ShiftId,
                CustomerId = customerId,
                CustomerName = SelectedCustomer?.Name ?? NewCustomerName,
                Lines = Lines,
                InvoiceDiscount = Totals.InvoiceDiscount,
                InvoiceDiscountPercentage = Totals.InvoiceDiscountPercentage,
                PaidAmount = IsCredit ? 0m : PaidAmount,
                PaymentMethod = SelectedMethod!.Method
            };

            Result = await _salesService.CompleteSaleAsync(request);
            Completed?.Invoke(this, Result);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _dialogs.ShowError(ex.Message, "تعذر إتمام البيع");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string Validate()
    {
        if (SelectedMethod is null)
        {
            return "يجب اختيار طريقة الدفع";
        }

        if (IsCredit && SelectedCustomer is null && string.IsNullOrWhiteSpace(NewCustomerName))
        {
            return "البيع الآجل يتطلب اختيار عميل مسجل أو إدخال اسم عميل جديد";
        }

        if (!IsCredit && PaidAmount < Totals.TotalAmount)
        {
            return $"المبلغ المدفوع أقل من الإجمالي. الباقي: {RemainingAmount:N2} {DomainConstants.CurrencySymbol}";
        }

        return string.Empty;
    }
}

/// <summary>Combo box / toggle item for the payment methods.</summary>
public sealed class PaymentMethodOption
{
    public PaymentMethodOption(PaymentMethod method, string caption)
    {
        Method = method;
        Caption = caption;
    }

    public PaymentMethod Method { get; }

    public string Caption { get; }

    public override string ToString() => Caption;
}

/// <summary>Quick cash denomination button.</summary>
public sealed class QuickCashOption
{
    public QuickCashOption(decimal amount, string caption)
    {
        Amount = amount;
        Caption = caption;
    }

    public decimal Amount { get; }

    public string Caption { get; }

    public override string ToString() => Caption;
}
