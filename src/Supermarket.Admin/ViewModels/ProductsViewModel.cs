using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Admin.Views;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Product management screen (Phase 2): DataGrid, free text search, category / stock filters,
/// low-stock highlighting and the create / edit / activate commands.
/// </summary>
public sealed partial class ProductsViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ICatalogService _catalogService;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    private CancellationTokenSource? _searchCts;

    public ProductsViewModel(
        IProductService productService,
        ICatalogService catalogService,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _productService = productService;
        _catalogService = catalogService;
        _dialogs = dialogs;
        _services = services;

        _ = InitializeAsync();
    }

    // ---------------------------------------------------------------- State
    public ObservableCollection<ProductDto> Products { get; } = new();

    [ObservableProperty]
    private ProductDto? selectedProduct;

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private LookupDto? selectedCategory;

    [ObservableProperty]
    private bool lowStockOnly;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int pageNumber = 1;

    [ObservableProperty]
    private int pageSize = 50;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private int totalPages;

    [ObservableProperty]
    private string sortBy = "Name";

    [ObservableProperty]
    private bool sortDescending;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public IReadOnlyList<LookupDto> Categories { get; private set; } = Array.Empty<LookupDto>();

    /// <summary>Always present first entry so the user can clear the category filter.</summary>
    public IReadOnlyList<LookupDto> CategoryFilterSource => Categories;

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Initial load: lookup lists + first page.</summary>
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            Categories = await _catalogService.GetCategoriesAsync();
            OnPropertyChanged(nameof(Categories));
            OnPropertyChanged(nameof(CategoryFilterSource));
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل المنتجات:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Reads the current page from the database using the active filters.</summary>
    [RelayCommand]
    private async Task LoadPageAsync()
    {
        if (IsBusy && Products.Count > 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var filter = new ProductFilter
            {
                SearchTerm = SearchTerm,
                CategoryId = SelectedCategory?.Id,
                LowStockOnly = LowStockOnly,
                PageNumber = PageNumber,
                PageSize = PageSize,
                SortBy = SortBy,
                SortDescending = SortDescending
            };

            var result = await _productService.GetPagedAsync(filter);

            Products.Clear();
            foreach (var item in result.Items)
            {
                Products.Add(item);
            }

            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
            StatusMessage = $"عدد المنتجات: {TotalCount:N0} - صفحة {result.PageNumber} من {Math.Max(1, result.TotalPages)}";
            NotifyPagingChanged();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل المنتجات:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// The search box triggers this command on every keystroke; the debounce cancels
    /// the previous query so only the last term reaches SQL Server.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(350, _searchCts.Token);
            PageNumber = 1;
            await LoadPageAsync();
        }
        catch (TaskCanceledException)
        {
            // Superseded by a newer keystroke: intentionally ignored.
        }
    }

    partial void OnSearchTermChanged(string value) => SearchCommand.Execute(null);

    partial void OnSelectedCategoryChanged(LookupDto? value)
    {
        PageNumber = 1;
        LoadPageCommand.Execute(null);
    }

    partial void OnLowStockOnlyChanged(bool value)
    {
        PageNumber = 1;
        LoadPageCommand.Execute(null);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!HasNextPage)
        {
            return;
        }

        PageNumber++;
        LoadPageCommand.Execute(null);
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!HasPreviousPage)
        {
            return;
        }

        PageNumber--;
        LoadPageCommand.Execute(null);
    }

    /// <summary>Sorts by the requested column, toggling the direction on a second click.</summary>
    [RelayCommand]
    private void Sort(string? column)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            return;
        }

        if (string.Equals(SortBy, column, StringComparison.OrdinalIgnoreCase))
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortBy = column;
            SortDescending = false;
        }

        LoadPageCommand.Execute(null);
    }

    /// <summary>Opens the create / edit dialog. An id of 0 creates a new product.</summary>
    [RelayCommand]
    private async Task OpenEditorAsync(ProductDto? product)
    {
        var dialog = new ProductEditWindow(_services);
        await dialog.InitializeAsync(product?.Id ?? 0);

        if (dialog.ShowDialog() == true)
        {
            await LoadPageAsync();
        }
    }

    /// <summary>Toggles the active flag of the selected product.</summary>
    [RelayCommand]
    private async Task ToggleActiveAsync(ProductDto? product)
    {
        product ??= SelectedProduct;
        if (product is null)
        {
            _dialogs.ShowWarning("من فضلك اختر منتجاً أولاً", "تنبيه");
            return;
        }

        var action = product.IsActive ? "إيقاف" : "تفعيل";
        if (!_dialogs.Confirm($"هل تريد {action} المنتج ({product.Name})؟", "تأكيد"))
        {
            return;
        }

        try
        {
            await _productService.SetActiveAsync(product.Id, !product.IsActive);
            await LoadPageAsync();
            SelectedProduct = Products.FirstOrDefault(p => p.Id == product.Id);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, "خطأ");
        }
    }

    /// <summary>Deletes the selected product after confirmation.</summary>
    [RelayCommand]
    private async Task DeleteProductAsync(ProductDto? product)
    {
        product ??= SelectedProduct;
        if (product is null)
        {
            _dialogs.ShowWarning("من فضلك اختر منتجاً أولاً", "تنبيه");
            return;
        }

        var message = product.StockQuantity > 0
            ? $"المنتج ({product.Name}) لديه رصيد حالي بالمخزن ({product.StockQuantity:N2}).\nهل أنت متأكد من حذف هذا المنتج؟"
            : $"هل أنت متأكد من حذف المنتج ({product.Name})؟";

        if (!_dialogs.Confirm(message, "تأكيد الحذف"))
        {
            return;
        }

        try
        {
            await _productService.DeleteAsync(product.Id);
            await LoadPageAsync();
            SelectedProduct = null;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر حذف المنتج:\n{ex.Message}", "خطأ");
        }
    }

    /// <summary>Opens the manual stock correction dialog for the selected product.</summary>
    [RelayCommand]
    private async Task AdjustStockAsync(ProductDto? product)
    {
        product ??= SelectedProduct;

        var dialog = new StockAdjustmentWindow(_services);
        await dialog.InitializeAsync(product?.Id);

        if (dialog.ShowDialog() == true)
        {
            await LoadPageAsync();
            SelectedProduct = Products.FirstOrDefault(p => p.Id == product?.Id);
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchTerm = string.Empty;
        SelectedCategory = null;
        LowStockOnly = false;
        PageNumber = 1;
        LoadPageCommand.Execute(null);
    }

    private void NotifyPagingChanged()
    {
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
    }
}
