using CommunityToolkit.Mvvm.ComponentModel;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;

namespace Supermarket.POS.ViewModels;

/// <summary>
/// One editable row of the POS cart. Wraps <see cref="CartLineDto"/> with change notification so the
/// totals panel and the DataGrid update as soon as the cashier touches a quantity or a discount.
/// </summary>
public sealed partial class CartLineViewModel : ObservableObject
{
    private readonly Action _changed;

    public CartLineViewModel(ProductDto product, decimal quantity, Action changed)
    {
        _changed = changed;
        ProductId = product.Id;
        ProductName = product.Name;
        Barcode = product.Barcode;
        UnitPrice = product.EffectivePrice;
        UnitCost = product.CostPrice;
        VatRate = product.VatRate;
        StockOnHand = product.StockQuantity;
        UnitName = product.UnitName;
        IsWeighted = product.IsWeighted;
        this.quantity = quantity;
    }

    public int ProductId { get; }

    public string ProductName { get; }

    public string Barcode { get; }

    public decimal UnitPrice { get; }

    public decimal UnitCost { get; }

    public decimal VatRate { get; }

    public string UnitName { get; }

    public bool IsWeighted { get; }

    /// <summary>Quantity available in the warehouse, used for the inline validation.</summary>
    public decimal StockOnHand { get; }

    [ObservableProperty]
    private decimal quantity;

    [ObservableProperty]
    private decimal discountAmount;

    /// <summary>Quantity × unit price − line discount (before VAT).</summary>
    public decimal LineTotal => Math.Round(Quantity * UnitPrice - DiscountAmount, 2);

    /// <summary>VAT amount of the line.</summary>
    public decimal TaxAmount => Math.Round(LineTotal * VatRate / 100m, 2);

    /// <summary>Line total including VAT.</summary>
    public decimal LineTotalWithTax => Math.Round(LineTotal + TaxAmount, 2);

    /// <summary>True when the requested quantity exceeds the on-hand stock.</summary>
    public bool ExceedsStock => Quantity > StockOnHand;

    /// <summary>Converts the row back into the shared DTO consumed by the sales service.</summary>
    public CartLineDto ToDto() => new()
    {
        ProductId = ProductId,
        ProductName = ProductName,
        Barcode = Barcode,
        UnitPrice = UnitPrice,
        UnitCost = UnitCost,
        Quantity = Quantity,
        DiscountAmount = DiscountAmount,
        VatRate = VatRate
    };

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(LineTotalWithTax));
        OnPropertyChanged(nameof(ExceedsStock));
        _changed();
    }

    partial void OnDiscountAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(LineTotalWithTax));
        _changed();
    }

    /// <summary>Applies a percentage discount to the line gross amount.</summary>
    public void ApplyDiscount(decimal percentage)
    {
        var gross = Math.Round(Quantity * UnitPrice, 2);
        DiscountAmount = Math.Round(gross * Math.Clamp(percentage, 0m, 100m) / 100m, 2);
    }

    /// <summary>Arabic unit price label used by the F3 command feedback.</summary>
    public string UnitPriceLabel => $"{UnitPrice:N2} {DomainConstants.CurrencySymbol}";
}
