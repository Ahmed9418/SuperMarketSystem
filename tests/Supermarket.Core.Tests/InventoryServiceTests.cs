using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>
/// Tests for the Phase 2 inventory services: stock movement history, manual corrections,
/// category and unit master data.
/// </summary>
public sealed class InventoryServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database;

    public InventoryServiceTests() => _database = new SqliteTestDatabase();

    private StockService CreateStockService() => new(_database.Context, TestServices.Logger<StockService>());

    private InventoryService CreateInventoryService() =>
        new(_database.Context, CreateStockService(), TestServices.Logger<InventoryService>());

    // ------------------------------------------------------------- Adjustments

    [Fact]
    public async Task AdjustAsync_IncreasesBalanceWritesLedgerAndSaves()
    {
        var inventory = CreateInventoryService();
        var product = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");
        var before = product.StockQuantity;

        var newBalance = await inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = 10m,
            Direction = StockAdjustmentDirection.Increase,
            MovementType = StockMovementType.AdjustmentIn,
            Reason = "جرد أسبوعي",
            PerformedBy = "tester"
        });

        Assert.Equal(before + 10m, newBalance);

        // The adjustment must be persisted immediately (SaveChangesAsync inside the service).
        await _database.Context.Entry(product).ReloadAsync();
        Assert.Equal(before + 10m, product.StockQuantity);

        var movement = await _database.Context.StockMovements
            .Where(m => m.ProductId == product.Id)
            .OrderByDescending(m => m.Id)
            .FirstAsync();

        Assert.Equal(10m, movement.Quantity);
        Assert.Equal(StockMovementType.AdjustmentIn, movement.MovementType);
        Assert.Equal("جرد أسبوعي", movement.Reason);
        Assert.Equal("tester", movement.PerformedBy);
        Assert.Equal("ADJ", movement.ReferenceNumber);
    }

    [Fact]
    public async Task AdjustAsync_DecreasesBalanceForDamage()
    {
        var inventory = CreateInventoryService();
        var product = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");
        var before = product.StockQuantity;

        var newBalance = await inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = 3m,
            Direction = StockAdjustmentDirection.Decrease,
            MovementType = StockMovementType.Damage,
            Reason = "تلف أثناء النقل"
        });

        Assert.Equal(before - 3m, newBalance);

        var movement = await _database.Context.StockMovements
            .Where(m => m.ProductId == product.Id)
            .OrderByDescending(m => m.Id)
            .FirstAsync();

        Assert.Equal(-3m, movement.Quantity);
    }

    [Fact]
    public async Task AdjustAsync_RejectsInsufficientStock()
    {
        var inventory = CreateInventoryService();
        var product = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = product.StockQuantity + 1m,
            Direction = StockAdjustmentDirection.Decrease,
            MovementType = StockMovementType.AdjustmentOut,
            Reason = "محاولة خصم أكبر من الرصيد"
        }));

        Assert.Contains("Insufficient stock", exception.Message);
    }

    [Fact]
    public async Task AdjustAsync_RequiresReasonAndPositiveQuantity()
    {
        var inventory = CreateInventoryService();
        var product = await _database.Context.Products.FirstAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = 5m,
            Direction = StockAdjustmentDirection.Increase,
            MovementType = StockMovementType.AdjustmentIn,
            Reason = "   "
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = 0m,
            Direction = StockAdjustmentDirection.Increase,
            MovementType = StockMovementType.AdjustmentIn,
            Reason = "كمية صفر"
        }));
    }

    [Fact]
    public async Task AdjustAsync_RejectsMovementTypeThatContradictsDirection()
    {
        var inventory = CreateInventoryService();
        var product = await _database.Context.Products.FirstAsync();

        // "Sale" is an outbound movement, it must not be used for an increase.
        await Assert.ThrowsAsync<ArgumentException>(() => inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = 2m,
            Direction = StockAdjustmentDirection.Increase,
            MovementType = StockMovementType.Sale,
            Reason = "اتجاه خاطئ"
        }));
    }

    // ---------------------------------------------------------------- History

    [Fact]
    public async Task GetMovementsAsync_ReturnsPagedRowsWithArabicType()
    {
        var inventory = CreateInventoryService();

        var page = await inventory.GetMovementsAsync(new StockMovementFilter { PageSize = 5 });

        Assert.Equal(5, page.Items.Count);
        Assert.True(page.TotalCount >= 25); // opening balances of the seeded products
        Assert.All(page.Items, m => Assert.False(string.IsNullOrWhiteSpace(m.MovementTypeArabic)));
        Assert.All(page.Items, m => Assert.False(string.IsNullOrWhiteSpace(m.ProductName)));
    }

    [Fact]
    public async Task GetMovementsAsync_FiltersByTypeAndDateRange()
    {
        var inventory = CreateInventoryService();
        var product = await _database.Context.Products.FirstAsync();

        await inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = 4m,
            Direction = StockAdjustmentDirection.Increase,
            MovementType = StockMovementType.AdjustmentIn,
            Reason = "اختبار الفلترة"
        });

        var filtered = await inventory.GetMovementsAsync(new StockMovementFilter
        {
            MovementType = StockMovementType.AdjustmentIn,
            FromDate = DateTime.Today.AddDays(-1),
            ToDate = DateTime.Today
        });

        Assert.NotEmpty(filtered.Items);
        Assert.All(filtered.Items, m => Assert.Equal(StockMovementType.AdjustmentIn, m.MovementType));
    }

    [Fact]
    public async Task GetProductMovementsAsync_ReturnsOnlyThatProduct()
    {
        var inventory = CreateInventoryService();
        var product = await _database.Context.Products.FirstAsync();

        var movements = await inventory.GetProductMovementsAsync(product.Id, 50);

        Assert.NotEmpty(movements);
        Assert.All(movements, m => Assert.Equal(product.Id, m.ProductId));
    }

    // ----------------------------------------------------------- Categories

    [Fact]
    public async Task CategoryService_CreatesUpdatesAndPreventsDuplicates()
    {
        var service = new CategoryService(_database.Context, TestServices.Logger<CategoryService>());

        var id = await service.CreateAsync(new CategoryDto { Name = "مستلزمات أطفال", DisplayOrder = 9 });
        Assert.True(id > 0);

        var created = await service.GetByIdAsync(id);
        Assert.NotNull(created);
        Assert.Equal("مستلزمات أطفال", created!.Name);

        created.Description = "حفاضات وأغذية أطفال";
        await service.UpdateAsync(created);

        var updated = await service.GetByIdAsync(id);
        Assert.Equal("حفاضات وأغذية أطفال", updated!.Description);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CategoryDto { Name = "مستلزمات أطفال" }));
    }

    [Fact]
    public async Task CategoryService_RefusesToDeactivateCategoryWithActiveProducts()
    {
        var service = new CategoryService(_database.Context, TestServices.Logger<CategoryService>());

        // Category 1 (ألبان وأجبان) still holds active seeded products.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetActiveAsync(1, isActive: false));

        Assert.Contains("لا يمكن إيقاف القسم", exception.Message);
    }

    [Fact]
    public async Task CategoryService_ListsProductCounts()
    {
        var service = new CategoryService(_database.Context, TestServices.Logger<CategoryService>());

        var categories = await service.GetAllAsync();

        Assert.Equal(8, categories.Count);
        Assert.Contains(categories, c => c.Name == "ألبان وأجبان" && c.ProductCount == 4);
    }

    // ---------------------------------------------------------------- Units

    [Fact]
    public async Task UnitService_CreatesAndUpdatesUnits()
    {
        var service = new UnitService(_database.Context, TestServices.Logger<UnitService>());

        var id = await service.CreateAsync(new UnitDto { Name = "علبة", AllowFractions = false });
        Assert.True(id > 0);

        var unit = await service.GetByIdAsync(id);
        Assert.NotNull(unit);
        Assert.Equal("علبة", unit!.Symbol); // symbol defaults to the unit name

        unit.AllowFractions = true;
        await service.UpdateAsync(unit);

        var updated = await service.GetByIdAsync(id);
        Assert.True(updated!.AllowFractions);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new UnitDto { Name = "علبة" }));
    }

    [Fact]
    public async Task UnitService_ReportsProductCounts()
    {
        var service = new UnitService(_database.Context, TestServices.Logger<UnitService>());

        var units = await service.GetAllAsync();

        Assert.Equal(6, units.Count);
        var kilogram = units.Single(u => u.Name == "كيلوجرام");
        Assert.True(kilogram.AllowFractions);
        Assert.True(kilogram.ProductCount > 0);
    }

    public void Dispose() => _database.Dispose();
}
