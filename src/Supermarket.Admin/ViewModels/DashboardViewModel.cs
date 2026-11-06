using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Home screen of the admin application. Displays the aggregated counters produced by
/// <see cref="IDashboardService"/> plus the live low-stock alert list.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly IProductService _productService;
    private readonly IDialogService _dialogs;

    public DashboardViewModel(IDashboardService dashboardService, IProductService productService, IDialogService dialogs)
    {
        _dashboardService = dashboardService;
        _productService = productService;
        _dialogs = dialogs;

        _ = LoadAsync();
    }

    [ObservableProperty]
    private DashboardSummaryDto summary = new();

    [ObservableProperty]
    private IReadOnlyList<ProductDto> lowStockProducts = Array.Empty<ProductDto>();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string lastUpdatedText = string.Empty;

    /// <summary>Executed on construction and by the refresh command.</summary>
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
            Summary = await _dashboardService.GetSummaryAsync();
            LowStockProducts = await _productService.GetLowStockAsync(8);
            LastUpdatedText = $"آخر تحديث: {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل بيانات لوحة التحكم:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
