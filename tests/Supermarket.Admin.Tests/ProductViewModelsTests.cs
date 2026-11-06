using Microsoft.EntityFrameworkCore;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Xunit;

namespace Supermarket.Admin.Tests;

public sealed class ProductViewModelsTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ProductEditViewModel_Initialize_GeneratesBarcodeForNewProduct()
    {
        _fixture.SignInAsAdmin();
        var vm = new ProductEditViewModel(
            _fixture.ProductService,
            _fixture.CatalogService,
            _fixture.StoreSettings,
            _fixture.Session,
            _fixture.Dialogs);

        await vm.InitializeAsync(0);

        Assert.False(vm.IsEditMode);
        Assert.Equal("إضافة منتج جديد", vm.Title);
        Assert.NotEmpty(vm.Barcode);
        Assert.NotEmpty(vm.Categories);
        Assert.NotEmpty(vm.Units);
        Assert.NotNull(vm.SelectedCategory);
        Assert.NotNull(vm.SelectedUnit);
    }

    [Fact]
    public async Task ProductEditViewModel_MarginText_CalculatesCorrectly()
    {
        _fixture.SignInAsAdmin();
        var vm = new ProductEditViewModel(
            _fixture.ProductService,
            _fixture.CatalogService,
            _fixture.StoreSettings,
            _fixture.Session,
            _fixture.Dialogs);

        await vm.InitializeAsync(0);

        vm.CostPrice = 10m;
        vm.SellingPrice = 15m;
        vm.DiscountPrice = null;

        // Margin = 15 - 10 = 5. Margin% = 5 / 15 * 100 = 33.3%
        Assert.Contains("5.00", vm.MarginText);
        Assert.Contains("33.3%", vm.MarginText);

        // When discount is applied: selling=15, discount=12 -> effective=12. Margin = 12 - 10 = 2 (16.7%)
        vm.DiscountPrice = 12m;
        Assert.Contains("2.00", vm.MarginText);
        Assert.Contains("16.7%", vm.MarginText);
    }

    [Fact]
    public async Task ProductEditViewModel_Validation_CatchesBusinessRuleViolations()
    {
        _fixture.SignInAsAdmin();
        var vm = new ProductEditViewModel(
            _fixture.ProductService,
            _fixture.CatalogService,
            _fixture.StoreSettings,
            _fixture.Session,
            _fixture.Dialogs);

        await vm.InitializeAsync(0);

        // 1. Missing name
        vm.ProductName = "   ";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("اسم المنتج مطلوب", vm.ErrorMessage);

        // 2. Selling price below cost price
        vm.ProductName = "شاي أسود";
        vm.CostPrice = 20m;
        vm.SellingPrice = 15m;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("أقل من سعر التكلفة", vm.ErrorMessage);

        // 3. Discount price >= selling price
        vm.CostPrice = 10m;
        vm.SellingPrice = 20m;
        vm.DiscountPrice = 22m;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("سعر الخصم يجب أن يكون أقل", vm.ErrorMessage);

        // 4. Negative stock
        vm.DiscountPrice = null;
        vm.InitialStock = -5m;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("لا يمكن أن تكون سالبة", vm.ErrorMessage);
    }

    [Fact]
    public async Task ProductEditViewModel_SaveNewProduct_PersistsToDatabase()
    {
        _fixture.SignInAsAdmin();
        var vm = new ProductEditViewModel(
            _fixture.ProductService,
            _fixture.CatalogService,
            _fixture.StoreSettings,
            _fixture.Session,
            _fixture.Dialogs);

        await vm.InitializeAsync(0);

        vm.ProductName = "منتج تجريبي للاختبار";
        vm.Barcode = "6223999999999";
        vm.CostPrice = 25m;
        vm.SellingPrice = 35m;
        vm.InitialStock = 10m;
        vm.MinimumStockLevel = 2m;
        vm.ReorderQuantity = 5m;

        var savedFired = false;
        vm.Saved += (_, _) => savedFired = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(savedFired);
        Assert.Empty(vm.ErrorMessage);

        var product = await _fixture.Database.Context.Products.SingleOrDefaultAsync(p => p.Barcode == "6223999999999");
        Assert.NotNull(product);
        Assert.Equal("منتج تجريبي للاختبار", product!.Name);
        Assert.Equal(10m, product.StockQuantity);

        // Opening balance movement should be logged
        var movement = await _fixture.Database.Context.StockMovements.SingleOrDefaultAsync(m => m.ProductId == product.Id);
        Assert.NotNull(movement);
        Assert.Equal(StockMovementType.Purchase, movement!.MovementType);
        Assert.Equal(10m, movement.Quantity);
    }

    [Fact]
    public async Task ProductsViewModel_FilterAndPaging_BehavesCorrectly()
    {
        _fixture.SignInAsAdmin();
        var vm = new ProductsViewModel(
            _fixture.ProductService,
            _fixture.CatalogService,
            _fixture.Dialogs,
            _fixture.ServiceProvider);

        // Allow async init to finish
        await Task.Delay(100);

        Assert.NotEmpty(vm.Products);
        Assert.True(vm.TotalCount > 0);
        Assert.NotEmpty(vm.Categories);

        // Search by keyword
        vm.SearchTerm = "لبن";
        await Task.Delay(600); // allow debounce and async load to complete
        Assert.All(vm.Products, p => Assert.Contains("لبن", p.Name));

        // Low stock filter
        vm.SearchTerm = string.Empty;
        vm.LowStockOnly = true;
        await Task.Delay(600);
        Assert.All(vm.Products, p => Assert.True(p.StockQuantity <= p.MinimumStockLevel));
    }
}

