using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Catalog management screen: categories and units in one page (two tabs in the view).
/// Both lists are edited inline through the small forms hosted under the grids.
/// </summary>
public sealed partial class CatalogViewModel : ObservableObject
{
    private readonly ICategoryService _categoryService;
    private readonly IUnitService _unitService;
    private readonly IDialogService _dialogs;
    private readonly AuthSession _session;

    public CatalogViewModel(ICategoryService categoryService, IUnitService unitService, IDialogService dialogs, AuthSession session)
    {
        _categoryService = categoryService;
        _unitService = unitService;
        _dialogs = dialogs;
        _session = session;

        _ = LoadAsync();
    }

    /// <summary>Delete buttons stay disabled without the catalog edit permission.</summary>
    public bool CanEdit => _session.Can(AppPermissions.CatalogEdit);

    // ------------------------------------------------------------- Categories
    public ObservableCollection<CategoryDto> Categories { get; } = new();

    public ObservableCollection<CategoryDto> ParentCategories { get; } = new();

    [ObservableProperty]
    private CategoryDto? selectedCategory;

    [ObservableProperty]
    private string categoryName = string.Empty;

    [ObservableProperty]
    private string? categoryDescription;

    [ObservableProperty]
    private CategoryDto? selectedParentCategory;

    [ObservableProperty]
    private int categoryDisplayOrder;

    [ObservableProperty]
    private bool categoryIsActive = true;

    [ObservableProperty]
    private bool isEditingCategory;

    // ------------------------------------------------------------------ Units
    public ObservableCollection<UnitDto> Units { get; } = new();

    [ObservableProperty]
    private UnitDto? selectedUnit;

    [ObservableProperty]
    private string unitName = string.Empty;

    [ObservableProperty]
    private string? unitSymbol;

    [ObservableProperty]
    private bool unitAllowFractions;

    [ObservableProperty]
    private bool isEditingUnit;

    // ----------------------------------------------------------------- Shared
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public string CategoryFormTitle => IsEditingCategory ? "تعديل القسم المحدد" : "إضافة قسم جديد";

    public string UnitFormTitle => IsEditingUnit ? "تعديل الوحدة المحددة" : "إضافة وحدة جديدة";

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
            var categories = await _categoryService.GetAllAsync();
            var units = await _unitService.GetAllAsync();

            Categories.Clear();
            ParentCategories.Clear();
            ParentCategories.Add(new CategoryDto { Id = 0, Name = "بدون قسم رئيسي" });

            foreach (var category in categories)
            {
                Categories.Add(category);
                ParentCategories.Add(category);
            }

            Units.Clear();
            foreach (var unit in units)
            {
                Units.Add(unit);
            }
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل بيانات الأقسام والوحدات:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ------------------------------------------------------- Category commands
    partial void OnSelectedCategoryChanged(CategoryDto? value)
    {
        if (value is null)
        {
            return;
        }

        CategoryName = value.Name;
        CategoryDescription = value.Description;
        CategoryDisplayOrder = value.DisplayOrder;
        CategoryIsActive = value.IsActive;
        SelectedParentCategory = ParentCategories.FirstOrDefault(c => c.Id == (value.ParentCategoryId ?? 0));
        IsEditingCategory = true;
        OnPropertyChanged(nameof(CategoryFormTitle));
    }

    [RelayCommand]
    private void NewCategory()
    {
        SelectedCategory = null;
        CategoryName = string.Empty;
        CategoryDescription = null;
        CategoryDisplayOrder = Categories.Count + 1;
        CategoryIsActive = true;
        SelectedParentCategory = ParentCategories.FirstOrDefault();
        IsEditingCategory = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(CategoryFormTitle));
    }

    [RelayCommand]
    private async Task SaveCategoryAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            var dto = new CategoryDto
            {
                Id = IsEditingCategory ? SelectedCategory?.Id ?? 0 : 0,
                Name = CategoryName,
                Description = CategoryDescription,
                DisplayOrder = CategoryDisplayOrder,
                IsActive = CategoryIsActive,
                ParentCategoryId = SelectedParentCategory is { Id: > 0 } parent ? parent.Id : null
            };

            if (IsEditingCategory)
            {
                await _categoryService.UpdateAsync(dto);
                _dialogs.ShowInformation("تم تحديث بيانات القسم", "تم الحفظ");
            }
            else
            {
                await _categoryService.CreateAsync(dto);
                _dialogs.ShowInformation("تم إضافة القسم بنجاح", "تم الحفظ");
            }

            await LoadAsync();
            NewCategory();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _dialogs.ShowError(ex.Message, "تعذر الحفظ");
        }
    }

    [RelayCommand]
    private async Task ToggleCategoryActiveAsync(CategoryDto? category)
    {
        category ??= SelectedCategory;
        if (category is null)
        {
            _dialogs.ShowWarning("من فضلك اختر قسماً أولاً", "تنبيه");
            return;
        }

        try
        {
            await _categoryService.SetActiveAsync(category.Id, !category.IsActive);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, "تعذر تنفيذ العملية");
        }
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryDto? category)
    {
        category ??= SelectedCategory;
        if (category is null)
        {
            _dialogs.ShowWarning("من فضلك اختر قسماً أولاً", "تنبيه");
            return;
        }

        if (!_session.Can(AppPermissions.CatalogEdit))
        {
            _dialogs.ShowError("ليس لديك صلاحية حذف الأقسام.", "غير مسموح");
            return;
        }

        var productNote = category.ProductCount > 0
            ? $"\n\nتنبيه: {category.ProductCount} منتج بداخل هذا القسم — سيتم نقلها إلى قسم (عام) تلقائياً."
            : string.Empty;

        if (!_dialogs.Confirm($"هل تريد حذف القسم ({category.Name})؟ لا يمكن التراجع عن هذه العملية.{productNote}", "تأكيد الحذف"))
        {
            return;
        }

        try
        {
            await _categoryService.DeleteAsync(category.Id);
            SelectedCategory = null;
            await LoadAsync();
            NewCategory();
            _dialogs.ShowInformation($"تم حذف القسم ({category.Name})", "تم الحذف");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, "تعذر الحذف");
        }
    }

    // ----------------------------------------------------------- Unit commands
    partial void OnSelectedUnitChanged(UnitDto? value)
    {
        if (value is null)
        {
            return;
        }

        UnitName = value.Name;
        UnitSymbol = value.Symbol;
        UnitAllowFractions = value.AllowFractions;
        IsEditingUnit = true;
        OnPropertyChanged(nameof(UnitFormTitle));
    }

    [RelayCommand]
    private void NewUnit()
    {
        SelectedUnit = null;
        UnitName = string.Empty;
        UnitSymbol = null;
        UnitAllowFractions = false;
        IsEditingUnit = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(UnitFormTitle));
    }

    [RelayCommand]
    private async Task SaveUnitAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            var dto = new UnitDto
            {
                Id = IsEditingUnit ? SelectedUnit?.Id ?? 0 : 0,
                Name = UnitName,
                Symbol = UnitSymbol,
                AllowFractions = UnitAllowFractions
            };

            if (IsEditingUnit)
            {
                await _unitService.UpdateAsync(dto);
                _dialogs.ShowInformation("تم تحديث بيانات الوحدة", "تم الحفظ");
            }
            else
            {
                await _unitService.CreateAsync(dto);
                _dialogs.ShowInformation("تم إضافة الوحدة بنجاح", "تم الحفظ");
            }

            await LoadAsync();
            NewUnit();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _dialogs.ShowError(ex.Message, "تعذر الحفظ");
        }
    }

    [RelayCommand]
    private async Task DeleteUnitAsync(UnitDto? unit)
    {
        unit ??= SelectedUnit;
        if (unit is null)
        {
            _dialogs.ShowWarning("من فضلك اختر وحدة قياس أولاً", "تنبيه");
            return;
        }

        if (!_session.Can(AppPermissions.CatalogEdit))
        {
            _dialogs.ShowError("ليس لديك صلاحية حذف وحدات القياس.", "غير مسموح");
            return;
        }

        if (!_dialogs.Confirm($"هل تريد حذف وحدة القياس ({unit.Name})؟", "تأكيد الحذف"))
        {
            return;
        }

        try
        {
            await _unitService.DeleteAsync(unit.Id);
            SelectedUnit = null;
            await LoadAsync();
            NewUnit();
            _dialogs.ShowInformation($"تم حذف الوحدة ({unit.Name})", "تم الحذف");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message, "تعذر الحذف");
        }
    }
}
