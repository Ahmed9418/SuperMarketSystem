using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>
/// Verifies the Phase 1 deliverables: the model is valid, the seed data is present and the
/// Arabic strings are stored as Unicode.
/// </summary>
public sealed class ModelAndSeedTests : IDisposable
{
    private readonly SqliteTestDatabase _database;

    public ModelAndSeedTests() => _database = new SqliteTestDatabase();

    [Fact]
    public void Model_IsValid_AndCreatesAllTables()
    {
        var creator = _database.Context.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        Assert.True(creator.Exists());

        // Every table of the Phase 1 model must physically exist in the generated schema.
        foreach (var table in new[]
                 {
                     "Products", "Categories", "Units", "Suppliers", "Customers",
                     "StockMovements", "PriceHistories", "StockCounts", "StockCountItems",
                     "Invoices", "InvoiceItems", "Payments", "Shifts", "ShiftReports",
                     "PurchaseOrders", "PurchaseOrderItems", "Users", "UserPermissions", "StoreSettings"
                 })
        {
            Assert.True(TableExists(table), $"Table '{table}' was not created.");
        }
    }

    private bool TableExists(string tableName)
    {
        var connection = _database.Context.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    [Fact]
    public async Task Seed_ContainsUsersUnitsCategoriesSuppliersAndProducts()
    {
        var db = _database.Context;

        Assert.Equal(4, await db.Users.CountAsync());
        Assert.Equal(6, await db.Units.CountAsync());
        Assert.Equal(8, await db.Categories.CountAsync());
        Assert.Equal(4, await db.Suppliers.CountAsync());
        Assert.True(await db.Products.CountAsync() >= 25);
        Assert.Equal(1, await db.StoreSettings.CountAsync());
        Assert.Equal(1, await db.Shifts.CountAsync());
    }

    [Fact]
    public async Task Seed_StockLedgerReconcilesWithProductQuantities()
    {
        var db = _database.Context;

        // Invariant: sum of the ledger rows == cached quantity on the product row.
        var ledgerTotals = await db.StockMovements
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(m => m.Quantity) })
            .ToListAsync();

        var products = await db.Products.Select(p => new { p.Id, p.StockQuantity }).ToListAsync();

        Assert.Equal(products.Count, ledgerTotals.Count);
        foreach (var product in products)
        {
            var ledger = ledgerTotals.Single(t => t.ProductId == product.Id);
            Assert.Equal(product.StockQuantity, ledger.Total);
        }
    }

    [Fact]
    public async Task Seed_StoresArabicTextAsUnicode()
    {
        var db = _database.Context;

        var product = await db.Products.FirstAsync(p => p.Barcode == "6223000011111");
        Assert.Equal("لبن جهينة كامل الدسم 1 لتر", product.Name);

        var category = await db.Categories.FirstAsync(c => c.Id == 1);
        Assert.Equal("ألبان وأجبان", category.Name);

        var user = await db.Users.FirstAsync(u => u.UserName == "admin");
        Assert.Equal("أحمد عبد الرحمن", user.FullName);
    }

    [Fact]
    public async Task Seed_UsersHaveHashedPasswordsAndPins()
    {
        var db = _database.Context;
        var admin = await db.Users.FirstAsync(u => u.UserName == "admin");

        Assert.StartsWith("AQAAAA", admin.PasswordHash);
        Assert.NotNull(admin.PinHash);
        Assert.DoesNotContain("Admin@123", admin.PasswordHash);

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Supermarket.Core.Entities.User>();
        Assert.NotEqual(
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(admin, admin.PasswordHash, "Admin@123"));
    }

    [Fact]
    public void DecimalAndStringPrecisions_AreConfigured()
    {
        var db = _database.Context;
        var productEntity = db.Model.FindEntityType(typeof(Supermarket.Core.Entities.Product))!;

        var sellingPrice = productEntity.FindProperty(nameof(Supermarket.Core.Entities.Product.SellingPrice))!;
        Assert.Equal(18, sellingPrice.GetPrecision());
        Assert.Equal(4, sellingPrice.GetScale());

        var name = productEntity.FindProperty(nameof(Supermarket.Core.Entities.Product.Name))!;
        Assert.True(name.IsUnicode());
        Assert.Equal(200, name.GetMaxLength());

        var barcode = productEntity.FindProperty(nameof(Supermarket.Core.Entities.Product.Barcode))!;
        Assert.False(barcode.IsUnicode());
    }

    [Fact]
    public async Task SoftDelete_HidesRowsFromQueries()
    {
        var db = _database.Context;
        var product = await db.Products.FirstAsync();
        var id = product.Id;

        db.Products.Remove(product);
        await db.SaveChangesAsync();

        Assert.Null(await db.Products.FirstOrDefaultAsync(p => p.Id == id));
        Assert.NotNull(await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id));
    }

    public void Dispose() => _database.Dispose();
}
