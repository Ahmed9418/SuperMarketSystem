using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Admin.Views;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Dedicated screen for the low-stock alerts: shows every active product whose on-hand
/// quantity reached the alert threshold, worst first, with the suggested reorder quantity.
/// </summary>
public sealed partial class LowStockViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    public LowStockViewModel(
        IProductService productService,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _productService = productService;
        _dialogs = dialogs;
        _services = services;

        _ = LoadAsync();
    }

    public ObservableCollection<ProductDto> Products { get; } = new();

    [ObservableProperty]
    private ProductDto? selectedProduct;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string summaryText = string.Empty;

    [ObservableProperty]
    private decimal estimatedReorderCost;

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
            var items = await _productService.GetLowStockAsync(200);

            Products.Clear();
            foreach (var item in items)
            {
                Products.Add(item);
            }

            var outOfStock = items.Count(p => p.StockQuantity <= 0m);
            SummaryText = $"إجمالي المنتجات الواقعة تحت حد الطلب: {items.Count:N0} - منها {outOfStock:N0} غير متوفر";
            EstimatedReorderCost = items.Sum(p => Math.Max(p.ReorderQuantity, p.MinimumStockLevel) * p.CostPrice);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل قائمة النواقص:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opens the stock receiving dialog pre-filled with the suggested reorder quantity and purchase movement type.
    /// </summary>
    [RelayCommand]
    private async Task ReceiveStockAsync(ProductDto? product)
    {
        product ??= SelectedProduct;
        if (product is null)
        {
            _dialogs.ShowWarning("من فضلك اختر منتجاً من القائمة أولاً", "تنبيه");
            return;
        }

        var dialog = new StockAdjustmentWindow(_services);
        var suggestedQty = product.ReorderQuantity > 0
            ? product.ReorderQuantity
            : Math.Max(1, product.MinimumStockLevel - product.StockQuantity);

        await dialog.InitializeAsync(
            product.Id,
            StockMovementType.Purchase,
            suggestedQty,
            "توريد نواقص مخزون");

        if (dialog.ShowDialog() == true)
        {
            await LoadAsync();
        }
    }
}
