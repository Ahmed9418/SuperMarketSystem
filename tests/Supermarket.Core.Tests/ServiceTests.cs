using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>Behavioural tests for the Phase 1 shared services.</summary>
public sealed class ServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database;

    public ServiceTests() => _database = new SqliteTestDatabase();

    private StockService CreateStockService() => new(_database.Context, TestServices.Logger<StockService>());

    private Task<ProductService> CreateProductServiceAsync()
    {
        var settings = new StoreSettingsService(new TestContextFactory(_database));
        return Task.FromResult(new ProductService(
            _database.Context,
            CreateStockService(),
            settings,
            TestServices.Logger<ProductService>()));
    }

    [Fact]
    public async Task AdjustStock_KeepsProductQuantityAndLedgerInSync()
    {
        var stock = CreateStockService();
        var product = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");
        var before = product.StockQuantity;

        await stock.AdjustStockAsync(product.Id, -5m, StockMovementType.Sale, "INV-TEST-1", performedBy: "tester");
        await _database.Context.SaveChangesAsync();

        await _database.Context.Entry(product).ReloadAsync();
        Assert.Equal(before - 5m, product.StockQuantity);

        var movement = await _database.Context.StockMovements
            .Where(m => m.ProductId == product.Id)
            .OrderByDescending(m => m.Id)
            .FirstAsync();

        Assert.Equal(-5m, movement.Quantity);
        Assert.Equal(before - 5m, movement.BalanceAfter);
        Assert.Equal(StockMovementType.Sale, movement.MovementType);
    }

    [Fact]
    public async Task AdjustStock_RejectsNegativeBalance()
    {
        var stock = CreateStockService();
        var product = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223000011111");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stock.AdjustStockAsync(product.Id, -(product.StockQuantity + 1m), StockMovementType.AdjustmentOut));
    }

    [Fact]
    public async Task FindByBarcode_ReturnsProductAndArabicMessage()
    {
        var service = await CreateProductServiceAsync();

        var result = await service.FindByBarcodeAsync("6223000011111");

        Assert.True(result.Found);
        Assert.NotNull(result.Product);
        Assert.Contains("ج.م", result.Message);
        Assert.Contains("لبن", result.Product!.Name);
    }

    [Fact]
    public async Task FindByBarcode_ReportsUnknownCodeInArabic()
    {
        var service = await CreateProductServiceAsync();

        var result = await service.FindByBarcodeAsync("0000000000000");

        Assert.False(result.Found);
        Assert.Contains("غير مسجل", result.Message);
    }

    [Fact]
    public async Task GetPaged_FiltersBySearchTermAndLowStock()
    {
        var service = await CreateProductServiceAsync();

        var search = await service.GetPagedAsync(new ProductFilter { SearchTerm = "لبن", PageSize = 50 });
        Assert.Contains(search.Items, p => p.Name.Contains("لبن"));

        var lowStock = await service.GetPagedAsync(new ProductFilter { LowStockOnly = true, PageSize = 100 });
        Assert.NotEmpty(lowStock.Items);
        Assert.All(lowStock.Items, p => Assert.True(p.StockQuantity <= p.MinimumStockLevel));
    }

    [Fact]
    public async Task CreateAsync_WritesOpeningStockMovement()
    {
        var service = await CreateProductServiceAsync();

        var id = await service.CreateAsync(new ProductCreateUpdateDto
        {
            Name = "عصير برتقال طبيعي 1 لتر",
            CategoryId = 3,
            UnitId = 4,
            SupplierId = 3,
            CostPrice = 15m,
            SellingPrice = 21m,
            VatRate = 14m,
            MinimumStockLevel = 5m,
            ReorderQuantity = 10m,
            InitialStock = 12m,
            ChangedBy = "tester"
        });

        var created = await service.GetByIdAsync(id);
        Assert.NotNull(created);
        Assert.Equal(12m, created!.StockQuantity);

        var movement = await _database.Context.StockMovements
            .Where(m => m.ProductId == id)
            .SingleAsync();

        Assert.Equal(StockMovementType.Purchase, movement.MovementType);
        Assert.Equal(12m, movement.Quantity);
        Assert.Equal("رصيد افتتاحي", movement.Reason);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateBarcode()
    {
        var service = await CreateProductServiceAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new ProductCreateUpdateDto
        {
            Name = "منتج مقلد",
            Barcode = "6223000011111",
            CategoryId = 1,
            UnitId = 1,
            SellingPrice = 10m
        }));
    }

    [Fact]
    public async Task UpdateAsync_RecordsPriceHistory()
    {
        var service = await CreateProductServiceAsync();
        var product = await _database.Context.Products.FirstAsync();
        var dto = new ProductCreateUpdateDto
        {
            Id = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            CategoryId = product.CategoryId,
            UnitId = product.UnitId,
            CostPrice = product.CostPrice + 1m,
            SellingPrice = product.SellingPrice + 2m,
            VatRate = product.VatRate,
            ChangedBy = "tester"
        };

        var originalCost = product.CostPrice;

        await service.UpdateAsync(dto);

        var history = await _database.Context.PriceHistories.SingleAsync(h => h.ProductId == product.Id);
        Assert.Equal(originalCost, history.OldCostPrice);
        Assert.Equal(originalCost + 1m, history.NewCostPrice);
        Assert.Equal("tester", history.ChangedBy);
    }

    [Fact]
    public async Task GenerateBarcode_FollowsInternalPrefixAndStaysUnique()
    {
        var service = await CreateProductServiceAsync();

        var generated = await service.GenerateBarcodeAsync();

        Assert.StartsWith(DomainConstants.LooseItemBarcodePrefix, generated);
        Assert.Equal(13, generated.Length);

        // The generated value must never collide with an existing barcode.
        Assert.False(await service.IsBarcodeTakenAsync(generated));

        // ... but it is unique by construction, so creating with it must succeed.
        var id = await service.CreateAsync(new ProductCreateUpdateDto
        {
            Name = "منتج بالبار كود المولّد",
            Barcode = generated,
            CategoryId = 4,
            UnitId = 1,
            SellingPrice = 12m
        });

        Assert.True(id > 0);
        Assert.True(await service.IsBarcodeTakenAsync(generated));
    }

    [Fact]
    public async Task CategoryAndUnitLookups_ReturnArabicNames()
    {
        var catalog = new CatalogService(_database.Context, TestServices.Logger<CatalogService>());

        var categories = await catalog.GetCategoriesAsync();
        var units = await catalog.GetUnitsAsync();
        var suppliers = await catalog.GetSuppliersAsync();

        Assert.Equal(8, categories.Count);
        Assert.Contains(categories, c => c.Name == "ألبان وأجبان");
        Assert.Contains(units, u => u.Name == "كيلوجرام");
        Assert.Equal(4, suppliers.Count);
    }

    [Fact]
    public async Task AuthService_AcceptsPasswordAndPin_AndRejectsWrongOnes()
    {
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Supermarket.Core.Entities.User>();
        var auth = new AuthService(
            _database.Context,
            hasher,
            new FixedClock(new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc)),
            TestServices.Logger<AuthService>());

        var passwordSignIn = await auth.SignInAsync("admin", "Admin@123");
        Assert.True(passwordSignIn.Succeeded);
        Assert.Equal(UserRole.Administrator, passwordSignIn.User!.Role);
        Assert.Equal("مدير النظام", passwordSignIn.User.RoleArabic);

        var pinSignIn = await auth.SignInWithPinAsync("cashier", "2222");
        Assert.True(pinSignIn.Succeeded);

        var wrong = await auth.SignInAsync("admin", "wrong-password");
        Assert.False(wrong.Succeeded);
        Assert.NotNull(wrong.ErrorMessage);
    }

    [Fact]
    public async Task NumberSequence_ProducesSequentialDocumentNumbers()
    {
        var sequence = new NumberSequenceService(
            new TestContextFactory(_database),
            new FixedClock(new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc)));

        var invoice1 = await sequence.NextInvoiceNumberAsync();
        Assert.Equal("INV-20261105-0001", invoice1);

        _database.Context.Invoices.Add(new Supermarket.Core.Entities.Invoice
        {
            InvoiceNumber = invoice1,
            UserId = 2,
            TotalAmount = 100m,
            PaidAmount = 100m,
            Status = InvoiceStatus.Completed
        });
        await _database.Context.SaveChangesAsync();

        var invoice2 = await sequence.NextInvoiceNumberAsync();
        Assert.Equal("INV-20261105-0002", invoice2);

        var shift = await sequence.NextShiftNumberAsync();
        Assert.StartsWith("SH-20261105-", shift);
    }

    [Fact]
    public async Task Dashboard_ReturnsCountersFromSqlAggregates()
    {
        var dashboard = new DashboardService(
            _database.Context,
            new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));

        var summary = await dashboard.GetSummaryAsync();

        Assert.Equal(25, summary.TotalProducts);
        Assert.Equal(8, summary.TotalCategories);
        Assert.Equal(4, summary.TotalSuppliers);
        Assert.Equal(4, summary.TotalUsers);
        Assert.True(summary.TotalStockValue > 0m);
        Assert.True(summary.LowStockProducts >= 2); // the seeded zero-stock items
        Assert.Equal(1, summary.OpenShifts);
    }

    [Fact]
    public async Task SupplierService_CreatesAndUpdatesSupplier()
    {
        var service = new SupplierService(_database.Context, TestServices.Logger<SupplierService>());

        var id = await service.CreateAsync(new SupplierDto
        {
            Name = "شركة الوفاء للتوريدات",
            ContactPerson = "سيد علي",
            PhoneNumber = "01234567890",
            IsActive = true
        });

        var created = await service.GetByIdAsync(id);
        Assert.NotNull(created);
        Assert.Equal("شركة الوفاء للتوريدات", created!.Name);

        created.Balance = 1500m;
        await service.UpdateAsync(created);

        var updated = await service.GetByIdAsync(id);
        Assert.Equal(1500m, updated!.Balance);
    }

    // ------------------------------------------------------------- Deletes (Phase 5)
    [Fact]
    public async Task CategoryService_Delete_MovesProductsToFallbackCategory()
    {
        var service = new CategoryService(_database.Context, TestServices.Logger<CategoryService>());

        var categoryId = await service.CreateAsync(new CategoryDto { Name = "قسم للحذف", DisplayOrder = 90 });
        var unitId = await _database.Context.Units.Select(u => u.Id).FirstAsync();

        var product = new Supermarket.Core.Entities.Product
        {
            Name = "منتج داخل قسم محذوف",
            Barcode = "6223999000001",
            CategoryId = categoryId,
            UnitId = unitId,
            CostPrice = 10m,
            SellingPrice = 12m,
            StockQuantity = 5m
        };
        _database.Context.Products.Add(product);
        await _database.Context.SaveChangesAsync();

        await service.DeleteAsync(categoryId);

        // The category is gone (soft deleted) ...
        Assert.Null(await service.GetByIdAsync(categoryId));

        // ... but the product survived, re-homed in the "عام" fallback category.
        var reloaded = await _database.Context.Products.FirstAsync(p => p.Barcode == "6223999000001");
        var fallback = await _database.Context.Categories.FirstAsync(c => c.Id == reloaded.CategoryId);
        Assert.Equal("عام", fallback.Name);
    }

    [Fact]
    public async Task CategoryService_Delete_IsRefusedWhileItHasSubCategories()
    {
        var service = new CategoryService(_database.Context, TestServices.Logger<CategoryService>());

        var parentId = await service.CreateAsync(new CategoryDto { Name = "قسم رئيسي", DisplayOrder = 91 });
        await service.CreateAsync(new CategoryDto { Name = "قسم فرعي", ParentCategoryId = parentId, DisplayOrder = 92 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(parentId));
    }

    [Fact]
    public async Task UnitService_Delete_IsRefusedWhileProductsUseIt()
    {
        var service = new UnitService(_database.Context, TestServices.Logger<UnitService>());
        var usedUnitId = await _database.Context.Products.Select(p => p.UnitId).FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(usedUnitId));
    }

    [Fact]
    public async Task SupplierService_Delete_UnlinksProductsAndSoftDeletes()
    {
        var service = new SupplierService(_database.Context, TestServices.Logger<SupplierService>());
        var supplierId = await _database.Context.Products
            .Where(p => p.SupplierId != null)
            .Select(p => p.SupplierId!.Value)
            .FirstAsync();

        var productCount = await _database.Context.Products.CountAsync(p => p.SupplierId == supplierId);

        await service.DeleteAsync(supplierId);

        Assert.Null(await service.GetByIdAsync(supplierId));
        Assert.Equal(0, await _database.Context.Products.CountAsync(p => p.SupplierId == supplierId));
        Assert.True(productCount > 0);
    }

    public void Dispose() => _database.Dispose();
}
