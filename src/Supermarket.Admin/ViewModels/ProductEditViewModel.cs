using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// View model of the product create / edit dialog (Phase 2).
/// Performs the client side validation, then delegates persistence to <see cref="IProductService"/>.
/// </summary>
public sealed partial class ProductEditViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ICatalogService _catalogService;
    private readonly IStoreSettingsService _storeSettings;
    private readonly AuthSession _session;
    private readonly IDialogService _dialogs;

    private int _productId;

    public ProductEditViewModel(
        IProductService productService,
        ICatalogService catalogService,
        IStoreSettingsService storeSettings,
        AuthSession session,
        IDialogService dialogs)
    {
        _productService = productService;
        _catalogService = catalogService;
        _storeSettings = storeSettings;
        _session = session;
        _dialogs = dialogs;
    }

    // ------------------------------------------------------------ Form state
    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string barcode = string.Empty;

    [ObservableProperty]
    private string? alternativeBarcode;

    [ObservableProperty]
    private LookupDto? selectedCategory;

    [ObservableProperty]
    private LookupDto? selectedUnit;

    [ObservableProperty]
    private LookupDto? selectedSupplier;

    [ObservableProperty]
    private decimal costPrice;

    [ObservableProperty]
    private decimal sellingPrice;

    [ObservableProperty]
    private decimal? discountPrice;

    [ObservableProperty]
    private decimal vatRate;

    [ObservableProperty]
    private decimal minimumStockLevel;

    [ObservableProperty]
    private decimal reorderQuantity;

    [ObservableProperty]
    private decimal initialStock;

    [ObservableProperty]
    private string? shelfLocation;

    [ObservableProperty]
    private bool isWeighted;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    // --------------------------------------------------------------- Lookups
    public IReadOnlyList<LookupDto> Categories { get; private set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Units { get; private set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Suppliers { get; private set; } = Array.Empty<LookupDto>();

    /// <summary>
    /// Live margin preview shown under the price fields. Recalculated whenever one of the
    /// price fields changes so the admin immediately sees an unprofitable configuration.
    /// </summary>
    public string MarginText
    {
        get
        {
            var effectivePrice = DiscountPrice is > 0m && DiscountPrice < SellingPrice
                ? DiscountPrice!.Value
                : SellingPrice;

            var margin = effectivePrice - CostPrice;
            var percentage = effectivePrice <= 0m ? 0m : Math.Round(margin / effectivePrice * 100m, 1);

            return $"{margin:N2} {Supermarket.Core.Common.DomainConstants.CurrencySymbol} ({percentage:N1}%)";
        }
    }

    /// <summary>Dialog title, driven by the edit mode.</summary>
    public string Title => _productId == 0 ? "إضافة منتج جديد" : "تعديل بيانات المنتج";

    /// <summary>True when the dialog is editing an existing product.</summary>
    public bool IsEditMode => _productId > 0;

    /// <summary>Raised after a successful save so the dialog can close itself.</summary>
    public event EventHandler? Saved;

    /// <summary>Loads the lookup lists and, when an id is supplied, the product to edit.</summary>
    public async Task InitializeAsync(int productId)
    {
        _productId = productId;
        IsBusy = true;
        try
        {
            Categories = await _catalogService.GetCategoriesAsync();
            Units = await _catalogService.GetUnitsAsync();
            Suppliers = await _catalogService.GetSuppliersAsync();

            var settings = await _storeSettings.GetAsync();
            VatRate = settings.DefaultVatRate * 100m;

            if (productId > 0)
            {
                var product = await _productService.GetByIdAsync(productId);
                if (product is null)
                {
                    _dialogs.ShowError("لم يتم العثور على المنتج المطلوب", "خطأ");
                    return;
                }

                ProductName = product.Name;
                Description = product.Description;
                Barcode = product.Barcode;
                AlternativeBarcode = product.AlternativeBarcode;
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId);
                SelectedUnit = Units.FirstOrDefault(u => u.Id == product.UnitId);
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == product.SupplierId);
                CostPrice = product.CostPrice;
                SellingPrice = product.SellingPrice;
                DiscountPrice = product.DiscountPrice;
                VatRate = product.VatRate;
                MinimumStockLevel = product.MinimumStockLevel;
                ReorderQuantity = product.ReorderQuantity;
                ShelfLocation = product.ShelfLocation;
                IsWeighted = product.IsWeighted;
                IsActive = product.IsActive;
            }
            else
            {
                // Sensible defaults for a brand new product.
                SelectedCategory = Categories.FirstOrDefault();
                SelectedUnit = Units.FirstOrDefault();
                SelectedSupplier = Suppliers.FirstOrDefault();
                Barcode = await _productService.GenerateBarcodeAsync();
            }

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(IsEditMode));
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل بيانات الشاشة:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnCostPriceChanged(decimal value) => OnPropertyChanged(nameof(MarginText));

    partial void OnSellingPriceChanged(decimal value) => OnPropertyChanged(nameof(MarginText));

    partial void OnDiscountPriceChanged(decimal? value) => OnPropertyChanged(nameof(MarginText));

    /// <summary>Generates the next internal barcode into the form field.</summary>
    [RelayCommand]
    private async Task GenerateBarcodeAsync()
    {
        try
        {
            Barcode = await _productService.GenerateBarcodeAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, "خطأ");
        }
    }

    /// <summary>Validates and persists the product.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = Validate();
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var dto = new ProductCreateUpdateDto
            {
                Id = _productId,
                Name = ProductName.Trim(),
                Description = Description,
                Barcode = Barcode?.Trim(),
                AlternativeBarcode = AlternativeBarcode?.Trim(),
                CategoryId = SelectedCategory!.Id,
                UnitId = SelectedUnit!.Id,
                SupplierId = SelectedSupplier?.Id,
                CostPrice = CostPrice,
                SellingPrice = SellingPrice,
                DiscountPrice = DiscountPrice is > 0m ? DiscountPrice : null,
                VatRate = VatRate,
                MinimumStockLevel = MinimumStockLevel,
                ReorderQuantity = ReorderQuantity,
                InitialStock = IsEditMode ? 0m : InitialStock,
                ShelfLocation = ShelfLocation,
                IsWeighted = IsWeighted,
                IsActive = IsActive,
                ChangedBy = _session.CurrentUser?.UserName
            };

            if (IsEditMode)
            {
                await _productService.UpdateAsync(dto);
                _dialogs.ShowInformation("تم تحديث بيانات المنتج بنجاح", "تم الحفظ");
            }
            else
            {
                await _productService.CreateAsync(dto);
                _dialogs.ShowInformation("تم إضافة المنتج بنجاح", "تم الحفظ");
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

    /// <summary>Fields validated before any database call is attempted.</summary>
    private string Validate()
    {
        if (string.IsNullOrWhiteSpace(ProductName))
        {
            return "اسم المنتج مطلوب";
        }

        if (SelectedCategory is null)
        {
            return "يجب اختيار القسم";
        }

        if (SelectedUnit is null)
        {
            return "يجب اختيار وحدة القياس";
        }

        if (CostPrice < 0m)
        {
            return "سعر التكلفة لا يمكن أن يكون سالباً";
        }

        if (SellingPrice <= 0m)
        {
            return "سعر البيع يجب أن يكون أكبر من صفر";
        }

        if (SellingPrice < CostPrice)
        {
            return "سعر البيع أقل من سعر التكلفة، تأكد من الأسعار";
        }

        if (DiscountPrice is > 0m && DiscountPrice >= SellingPrice)
        {
            return "سعر الخصم يجب أن يكون أقل من سعر البيع";
        }

        if (VatRate is < 0m or > 100m)
        {
            return "نسبة الضريبة يجب أن تكون بين 0 و 100";
        }

        if (MinimumStockLevel < 0m || InitialStock < 0m)
        {
            return "الكميات لا يمكن أن تكون سالبة";
        }

        return string.Empty;
    }
}
