using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Manual stock correction dialog (تسوية مخزون).
/// The view model validates the direction / movement-type combination before the service is called
/// so the operator gets an immediate Arabic explanation instead of a database exception.
/// </summary>
public sealed partial class StockAdjustmentViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;
    private readonly AuthSession _session;
    private readonly IDialogService _dialogs;

    public StockAdjustmentViewModel(
        IInventoryService inventoryService,
        IProductService productService,
        AuthSession session,
        IDialogService dialogs)
    {
        _inventoryService = inventoryService;
        _productService = productService;
        _session = session;
        _dialogs = dialogs;
    }

    // ------------------------------------------------------------------ State
    [ObservableProperty]
    private string searchTerm = string.Empty;

    public IReadOnlyList<ProductDto> SearchResults { get; private set; } = Array.Empty<ProductDto>();

    [ObservableProperty]
    private ProductDto? selectedProduct;

    [ObservableProperty]
    private decimal currentStock;

    [ObservableProperty]
    private string unitName = string.Empty;

    public IReadOnlyList<AdjustmentOption> Directions { get; } = new[]
    {
        new AdjustmentOption(StockAdjustmentDirection.Increase, "زيادة الكمية (+)"),
        new AdjustmentOption(StockAdjustmentDirection.Decrease, "خصم الكمية (-)")
    };

    public IReadOnlyList<AdjustmentTypeOption> MovementTypes { get; } = new[]
    {
        new AdjustmentTypeOption(StockMovementType.AdjustmentIn, "تسوية بالزيادة", StockAdjustmentDirection.Increase),
        new AdjustmentTypeOption(StockMovementType.Purchase, "توريد", StockAdjustmentDirection.Increase),
        new AdjustmentTypeOption(StockMovementType.SaleReturn, "مرتجع بيع", StockAdjustmentDirection.Increase),
        new AdjustmentTypeOption(StockMovementType.StockCount, "جرد", StockAdjustmentDirection.Increase),
        new AdjustmentTypeOption(StockMovementType.AdjustmentOut, "تسوية بالنقص", StockAdjustmentDirection.Decrease),
        new AdjustmentTypeOption(StockMovementType.PurchaseReturn, "مرتجع توريد", StockAdjustmentDirection.Decrease),
        new AdjustmentTypeOption(StockMovementType.Damage, "تالف", StockAdjustmentDirection.Decrease)
    };

    [ObservableProperty]
    private AdjustmentOption? selectedDirection;

    [ObservableProperty]
    private AdjustmentTypeOption? selectedMovementType;

    [ObservableProperty]
    private decimal quantity;

    [ObservableProperty]
    private string? reason;

    [ObservableProperty]
    private string? referenceNumber;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>Live preview of the balance after the correction.</summary>
    public decimal BalanceAfter => SelectedDirection?.Direction == StockAdjustmentDirection.Increase
        ? CurrentStock + Quantity
        : CurrentStock - Quantity;

    /// <summary>Arabic hint describing what the selected movement type will do.</summary>
    public string DirectionHint => SelectedDirection?.Direction == StockAdjustmentDirection.Increase
        ? "سيتم إضافة الكمية إلى رصيد المخزون"
        : "سيتم خصم الكمية من رصيد المخزون";

    /// <summary>Raised after a successful adjustment.</summary>
    public event EventHandler? Saved;

    public async Task InitializeAsync(int? productId)
    {
        SelectedDirection = Directions[0];
        SelectedMovementType = MovementTypes[0];

        if (productId is > 0)
        {
            var product = await _productService.GetByIdAsync(productId.Value);
            if (product is not null)
            {
                SelectedProduct = product;
                CurrentStock = product.StockQuantity;
                UnitName = product.UnitName;
            }
        }
    }

    /// <summary>Search the catalog so the operator can pick a product without leaving the dialog.</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            SearchResults = await _productService.SearchAsync(SearchTerm, 30);
            OnPropertyChanged(nameof(SearchResults));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    partial void OnSelectedProductChanged(ProductDto? value)
    {
        if (value is null)
        {
            return;
        }

        CurrentStock = value.StockQuantity;
        UnitName = value.UnitName;
        OnPropertyChanged(nameof(BalanceAfter));
    }

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(BalanceAfter));

    partial void OnCurrentStockChanged(decimal value) => OnPropertyChanged(nameof(BalanceAfter));

    partial void OnSelectedDirectionChanged(AdjustmentOption? value)
    {
        OnPropertyChanged(nameof(BalanceAfter));
        OnPropertyChanged(nameof(DirectionHint));

        // Keep the movement type list consistent with the chosen direction.
        if (value is not null)
        {
            var matching = MovementTypes.FirstOrDefault(t => t.Direction == value.Direction);
            if (matching is not null && SelectedMovementType?.Direction != value.Direction)
            {
                SelectedMovementType = matching;
            }
        }
    }

    partial void OnSelectedMovementTypeChanged(AdjustmentTypeOption? value)
    {
        if (value is null)
        {
            return;
        }

        var direction = Directions.FirstOrDefault(d => d.Direction == value.Direction);
        if (direction is not null && SelectedDirection != direction)
        {
            SelectedDirection = direction;
        }
    }

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
            await _inventoryService.AdjustAsync(new StockAdjustmentRequest
            {
                ProductId = SelectedProduct!.Id,
                Quantity = Quantity,
                Direction = SelectedDirection!.Direction,
                MovementType = SelectedMovementType!.MovementType,
                Reason = Reason,
                ReferenceNumber = ReferenceNumber,
                PerformedBy = _session.CurrentUser?.UserName
            });

            _dialogs.ShowInformation(
                $"تم تسجيل التسوية بنجاح.\nالرصيد الجديد للمنتج ({SelectedProduct.Name}): {BalanceAfter:N2} {UnitName}",
                "تم الحفظ");

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _dialogs.ShowError(ex.Message, "تعذر تنفيذ التسوية");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string Validate()
    {
        if (SelectedProduct is null)
        {
            return "يجب اختيار المنتج المطلوب تسويته";
        }

        if (SelectedDirection is null || SelectedMovementType is null)
        {
            return "يجب اختيار نوع الحركة";
        }

        if (Quantity <= 0m)
        {
            return "الكمية يجب أن تكون أكبر من صفر";
        }

        if (SelectedDirection.Direction == StockAdjustmentDirection.Decrease && Quantity > CurrentStock)
        {
            return $"الكمية المطلوب خصمها أكبر من الرصيد المتاح ({CurrentStock:N2} {UnitName})";
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            return "سبب التسوية مطلوب";
        }

        return string.Empty;
    }
}

/// <summary>Combo box item for the adjustment direction.</summary>
public sealed class AdjustmentOption
{
    public AdjustmentOption(StockAdjustmentDirection direction, string caption)
    {
        Direction = direction;
        Caption = caption;
    }

    public StockAdjustmentDirection Direction { get; }

    public string Caption { get; }

    public override string ToString() => Caption;
}

/// <summary>Combo box item for the movement type, linked to its natural direction.</summary>
public sealed class AdjustmentTypeOption
{
    public AdjustmentTypeOption(StockMovementType movementType, string caption, StockAdjustmentDirection direction)
    {
        MovementType = movementType;
        Caption = caption;
        Direction = direction;
    }

    public StockMovementType MovementType { get; }

    public string Caption { get; }

    public StockAdjustmentDirection Direction { get; }

    public override string ToString() => Caption;
}
