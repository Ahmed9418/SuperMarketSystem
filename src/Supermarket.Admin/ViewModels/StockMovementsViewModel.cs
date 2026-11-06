using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Stock movement history (حركة المخزون) – the "stock card" of the store.
/// Supports free text search, movement type and date range filters plus paging.
/// </summary>
public sealed partial class StockMovementsViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IDialogService _dialogs;

    public StockMovementsViewModel(IInventoryService inventoryService, IDialogService dialogs)
    {
        _inventoryService = inventoryService;
        _dialogs = dialogs;

        _ = LoadAsync();
    }

    public ObservableCollection<StockMovementDto> Movements { get; } = new();

    /// <summary>Selectable movement types with an Arabic caption; null = all types.</summary>
    public IReadOnlyList<MovementTypeOption> MovementTypes { get; } = new[]
    {
        new MovementTypeOption(null, "كل الحركات"),
        new MovementTypeOption(StockMovementType.Purchase, "توريد"),
        new MovementTypeOption(StockMovementType.Sale, "بيع"),
        new MovementTypeOption(StockMovementType.SaleReturn, "مرتجع بيع"),
        new MovementTypeOption(StockMovementType.PurchaseReturn, "مرتجع توريد"),
        new MovementTypeOption(StockMovementType.AdjustmentIn, "تسوية بالزيادة"),
        new MovementTypeOption(StockMovementType.AdjustmentOut, "تسوية بالنقص"),
        new MovementTypeOption(StockMovementType.StockCount, "جرد"),
        new MovementTypeOption(StockMovementType.Damage, "تالف")
    };

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private MovementTypeOption? selectedMovementType;

    [ObservableProperty]
    private DateTime? fromDate = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime? toDate = DateTime.Today;

    [ObservableProperty]
    private bool inboundOnly;

    [ObservableProperty]
    private bool outboundOnly;

    [ObservableProperty]
    private int pageNumber = 1;

    [ObservableProperty]
    private int pageSize = 100;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private int totalPages;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private decimal totalInboundValue;

    [ObservableProperty]
    private decimal totalOutboundValue;

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

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
            var filter = new StockMovementFilter
            {
                SearchTerm = SearchTerm,
                MovementType = SelectedMovementType?.Value,
                FromDate = FromDate,
                ToDate = ToDate,
                PageNumber = PageNumber,
                PageSize = PageSize
            };

            var result = await _inventoryService.GetMovementsAsync(filter);

            // The in/out toggle is applied client side so it can be combined freely with the
            // server side filter list without another round trip to SQL Server.
            var items = result.Items
                .Where(m => !InboundOnly || m.IsInbound)
                .Where(m => !OutboundOnly || !m.IsInbound)
                .ToList();

            Movements.Clear();
            foreach (var movement in items)
            {
                Movements.Add(movement);
            }

            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
            TotalInboundValue = items.Where(m => m.IsInbound).Sum(m => m.MovementValue);
            TotalOutboundValue = items.Where(m => !m.IsInbound).Sum(m => m.MovementValue);
            StatusMessage = $"عدد الحركات: {TotalCount:N0} - صفحة {result.PageNumber} من {Math.Max(1, result.TotalPages)}";

            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل حركة المخزون:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task SearchAsync()
    {
        PageNumber = 1;
        return LoadAsync();
    }

    [RelayCommand]
    private Task ClearFiltersAsync()
    {
        SearchTerm = string.Empty;
        SelectedMovementType = MovementTypes[0];
        FromDate = DateTime.Today.AddDays(-30);
        ToDate = DateTime.Today;
        InboundOnly = false;
        OutboundOnly = false;
        PageNumber = 1;
        return LoadAsync();
    }

    [RelayCommand]
    private Task NextPageAsync()
    {
        if (!HasNextPage)
        {
            return Task.CompletedTask;
        }

        PageNumber++;
        return LoadAsync();
    }

    [RelayCommand]
    private Task PreviousPageAsync()
    {
        if (!HasPreviousPage)
        {
            return Task.CompletedTask;
        }

        PageNumber--;
        return LoadAsync();
    }
}

/// <summary>Wrapper that lets the combo box show an Arabic caption for a nullable movement type.</summary>
public sealed class MovementTypeOption
{
    public MovementTypeOption(StockMovementType? value, string caption)
    {
        Value = value;
        Caption = caption;
    }

    public StockMovementType? Value { get; }

    public string Caption { get; }

    public override string ToString() => Caption;
}
