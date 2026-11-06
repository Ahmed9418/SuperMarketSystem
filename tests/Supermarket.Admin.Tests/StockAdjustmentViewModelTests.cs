using Microsoft.EntityFrameworkCore;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Xunit;

namespace Supermarket.Admin.Tests;

public sealed class StockAdjustmentViewModelTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task StockAdjustmentViewModel_BalanceAfter_CalculatesCorrectly()
    {
        _fixture.SignInAsAdmin();
        var vm = new StockAdjustmentViewModel(
            _fixture.InventoryService,
            _fixture.ProductService,
            _fixture.Session,
            _fixture.Dialogs);

        var product = await _fixture.Database.Context.Products.FirstAsync(p => p.StockQuantity > 10m);
        await vm.InitializeAsync(product.Id);

        Assert.Equal(product.StockQuantity, vm.CurrentStock);

        // Increase
        vm.SelectedDirection = vm.Directions.First(d => d.Direction == StockAdjustmentDirection.Increase);
        vm.Quantity = 5m;
        Assert.Equal(product.StockQuantity + 5m, vm.BalanceAfter);

        // Decrease
        vm.SelectedDirection = vm.Directions.First(d => d.Direction == StockAdjustmentDirection.Decrease);
        vm.Quantity = 3m;
        Assert.Equal(product.StockQuantity - 3m, vm.BalanceAfter);
    }

    [Fact]
    public async Task StockAdjustmentViewModel_Validation_PreventsOverDeductionAndEmptyFields()
    {
        _fixture.SignInAsAdmin();
        var vm = new StockAdjustmentViewModel(
            _fixture.InventoryService,
            _fixture.ProductService,
            _fixture.Session,
            _fixture.Dialogs);

        var product = await _fixture.Database.Context.Products.FirstAsync(p => p.StockQuantity > 0m);
        await vm.InitializeAsync(product.Id);

        // 1. Missing movement type
        vm.SelectedDirection = vm.Directions.First(d => d.Direction == StockAdjustmentDirection.Decrease);
        vm.SelectedMovementType = null;
        vm.Quantity = 1m;
        vm.Reason = "تسوية";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("نوع الحركة", vm.ErrorMessage);

        // 2. Zero quantity
        vm.SelectedMovementType = vm.MovementTypes.First(m => m.Direction == StockAdjustmentDirection.Decrease);
        vm.Quantity = 0m;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("أكبر من صفر", vm.ErrorMessage);

        // 3. Deducting more than available stock
        vm.Quantity = product.StockQuantity + 10m;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("أكبر من الرصيد المتاح", vm.ErrorMessage);

        // 4. Missing reason
        vm.Quantity = 1m;
        vm.Reason = "   ";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("سبب التسوية مطلوب", vm.ErrorMessage);
    }

    [Fact]
    public async Task StockAdjustmentViewModel_Save_UpdatesStockAndRecordsMovement()
    {
        _fixture.SignInAsAdmin();
        var vm = new StockAdjustmentViewModel(
            _fixture.InventoryService,
            _fixture.ProductService,
            _fixture.Session,
            _fixture.Dialogs);

        var product = await _fixture.Database.Context.Products.FirstAsync(p => p.StockQuantity >= 5m);
        var initialStock = product.StockQuantity;
        await vm.InitializeAsync(product.Id);

        vm.SelectedDirection = vm.Directions.First(d => d.Direction == StockAdjustmentDirection.Decrease);
        vm.SelectedMovementType = vm.MovementTypes.First(m => m.MovementType == StockMovementType.Damage);
        vm.Quantity = 2m;
        vm.Reason = "عبوات تالفة أثناء النقل";
        vm.ReferenceNumber = "DMG-001";

        var savedFired = false;
        vm.Saved += (_, _) => savedFired = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(savedFired);
        Assert.Empty(vm.ErrorMessage);

        var updated = await _fixture.Database.Context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(initialStock - 2m, updated.StockQuantity);

        var movement = await _fixture.Database.Context.StockMovements
            .Where(m => m.ProductId == product.Id && m.MovementType == StockMovementType.Damage)
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(movement);
        Assert.Equal(-2m, movement!.Quantity);
        Assert.Equal("عبوات تالفة أثناء النقل", movement.Reason);
        Assert.Equal("DMG-001", movement.ReferenceNumber);
    }
}

