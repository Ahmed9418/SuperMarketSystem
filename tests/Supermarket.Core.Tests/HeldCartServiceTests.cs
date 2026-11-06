using Microsoft.EntityFrameworkCore;
using Supermarket.Core.DTOs;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>Phase 4: parking a basket (F5) and resuming it (Shift+F5).</summary>
public sealed class HeldCartServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database;

    public HeldCartServiceTests() => _database = new SqliteTestDatabase();

    private HeldCartService CreateService() =>
        new(_database.Context, TestServices.Logger<HeldCartService>());

    private static HoldCartRequest Request(int productId, decimal quantity = 2m, string? label = "عميل التوصيل") => new()
    {
        UserId = 2,
        ShiftId = 1,
        TerminalId = "POS-01",
        Label = label,
        EstimatedTotal = 100m,
        Lines = new[]
        {
            new CartLineDto
            {
                ProductId = productId,
                ProductName = "اسم قديم في السلة",
                Quantity = quantity,
                DiscountAmount = 0m
            }
        }
    };

    [Fact]
    public async Task HoldAsync_StoresTheBasketAndTheCounter()
    {
        var service = CreateService();
        var product = await _database.Context.Products.FirstAsync();

        var id = await service.HoldAsync(Request(product.Id));

        var held = await service.GetHeldAsync(2);
        Assert.Single(held);
        Assert.Equal(id, held[0].Id);
        Assert.Equal("عميل التوصيل", held[0].Label);
        Assert.Equal(1, held[0].LineCount);
        Assert.Equal(2m, held[0].TotalQuantity);
        Assert.Equal(1, await service.CountAsync(2));
        Assert.Equal(0, await service.CountAsync(3));   // another cashier sees nothing
    }

    [Fact]
    public async Task HoldAsync_GeneratesALabel_WhenNoneIsGiven()
    {
        var service = CreateService();
        var product = await _database.Context.Products.FirstAsync();

        await service.HoldAsync(Request(product.Id, label: "  "));

        var held = await service.GetHeldAsync(2);
        Assert.StartsWith("سلة معلقة -", held[0].Label);
    }

    [Fact]
    public async Task HoldAsync_RejectsAnEmptyBasket()
    {
        var service = CreateService();
        var request = Request(1);
        request.Lines = Array.Empty<CartLineDto>();

        await Assert.ThrowsAsync<ArgumentException>(() => service.HoldAsync(request));
    }

    [Fact]
    public async Task ResumeAsync_RepricesFromTheDatabaseAndDeletesTheRow()
    {
        var service = CreateService();
        var product = await _database.Context.Products.AsNoTracking().FirstAsync(p => p.DiscountPrice == null);

        var id = await service.HoldAsync(Request(product.Id));

        // The price changes while the basket is parked: the resumed basket must use the new price.
        await _database.Context.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.SellingPrice, product.SellingPrice + 5m));

        var resumed = await service.ResumeAsync(id);

        Assert.NotNull(resumed);
        Assert.Equal(product.Name, resumed!.Lines[0].ProductName);          // refreshed, not the stale caption
        Assert.Equal(product.SellingPrice + 5m, resumed.Lines[0].UnitPrice);
        Assert.Equal(product.VatRate, resumed.Lines[0].VatRate);
        Assert.True(resumed.Lines[0].IsAvailable);

        // A held cart can only be resumed once.
        Assert.Null(await service.ResumeAsync(id));
        Assert.Equal(0, await service.CountAsync(2));
    }

    [Fact]
    public async Task ResumeAsync_FlagsShortStockWithoutLosingTheLine()
    {
        var service = CreateService();
        var product = await _database.Context.Products.AsNoTracking().FirstAsync();

        var id = await service.HoldAsync(Request(product.Id, quantity: product.StockQuantity + 10m));
        var resumed = await service.ResumeAsync(id);

        Assert.NotNull(resumed);
        Assert.False(resumed!.Lines[0].IsAvailable);
        Assert.Equal(product.StockQuantity + 10m, resumed.Lines[0].Quantity);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheParkedBasket()
    {
        var service = CreateService();
        var product = await _database.Context.Products.FirstAsync();

        var id = await service.HoldAsync(Request(product.Id));

        Assert.True(await service.DeleteAsync(id));
        Assert.False(await service.DeleteAsync(id));
        Assert.Empty(await service.GetHeldAsync(2));
    }

    [Fact]
    public void Payload_RoundTripsAndSurvivesCorruption()
    {
        var payload = new HeldCartPayload
        {
            InvoiceDiscount = 12.5m,
            InvoiceDiscountPercentage = 0m,
            Lines = new List<HeldCartLineDto>
            {
                new() { ProductId = 7, ProductName = "جبنة رومي", Quantity = 1.5m, DiscountAmount = 2m }
            }
        };

        var restored = HeldCartPayload.Deserialize(payload.Serialize());

        Assert.Equal(12.5m, restored.InvoiceDiscount);
        Assert.Single(restored.Lines);
        Assert.Equal("جبنة رومي", restored.Lines[0].ProductName);   // Arabic is not escaped in the JSON

        var broken = HeldCartPayload.Deserialize("{ not json");
        Assert.Empty(broken.Lines);
        Assert.Equal(HeldCartPayload.CurrentVersion, broken.Version);
    }

    public void Dispose() => _database.Dispose();
}
