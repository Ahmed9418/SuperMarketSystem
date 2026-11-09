using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.POS.ViewModels;
using Xunit;

namespace Supermarket.POS.Tests;

public sealed class SalesViewModelTests : IDisposable
{
    private readonly PosTestFixture _fixture = new();

    [Fact]
    public async Task ProcessBarcode_WithValidBarcode_AddsProductToCart()
    {
        var vm = _fixture.CreateSalesViewModel();
        var cashier = new UserDto { Id = 2, FullName = "محمود السيد", Role = UserRole.Cashier };
        await vm.InitializeAsync(cashier, "POS-01");

        // Seeded product: 6223000011111 is Juhayna Milk 1L (27.00 LE)
        vm.BarcodeBuffer = "6223000011111";
        await vm.ProcessBarcodeCommand.ExecuteAsync(null);

        Assert.Single(vm.Cart);
        Assert.Equal("لبن جهينة كامل الدسم 1 لتر", vm.Cart[0].ProductName);
        Assert.Equal(1m, vm.Cart[0].Quantity);
        Assert.Equal(27m, vm.Cart[0].UnitPrice);
        Assert.Equal(27m, vm.Totals.TotalAmount);
        Assert.False(vm.IsCartEmpty);
    }

    [Fact]
    public async Task ProcessBarcode_ScannedTwice_IncrementsQuantity()
    {
        var vm = _fixture.CreateSalesViewModel();
        var cashier = new UserDto { Id = 2, FullName = "محمود السيد", Role = UserRole.Cashier };
        await vm.InitializeAsync(cashier, "POS-01");

        vm.BarcodeBuffer = "6223000011111";
        await vm.ProcessBarcodeCommand.ExecuteAsync(null);

        vm.BarcodeBuffer = "6223000011111";
        await vm.ProcessBarcodeCommand.ExecuteAsync(null);

        Assert.Single(vm.Cart);
        Assert.Equal(2m, vm.Cart[0].Quantity);
        Assert.Equal(54m, vm.Totals.TotalAmount);
    }

    [Fact]
    public async Task ProcessBarcode_WithUnknownBarcode_PlaysErrorAndDoesNotAddToCart()
    {
        var vm = _fixture.CreateSalesViewModel();
        var errorPlayed = false;
        vm.ErrorSound = () => errorPlayed = true;

        vm.BarcodeBuffer = "9999999999999";
        await vm.ProcessBarcodeCommand.ExecuteAsync(null);

        Assert.Empty(vm.Cart);
        Assert.True(errorPlayed);
        Assert.Contains("غير مسجل", vm.StatusMessage);
    }

    [Fact]
    public async Task SearchProducts_FiltersResultsByName()
    {
        var vm = _fixture.CreateSalesViewModel();
        var cashier = new UserDto { Id = 2, FullName = "محمود السيد", Role = UserRole.Cashier };
        await vm.InitializeAsync(cashier, "POS-01");

        vm.SearchTerm = "لبن";
        await vm.SearchProductsCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.SearchResults);
        Assert.All(vm.SearchResults, p => Assert.Contains("لبن", p.Name));
    }

    [Fact]
    public async Task AddProduct_FromTouchTile_AddsToCart()
    {
        var vm = _fixture.CreateSalesViewModel();
        var product = await _fixture.ProductService.GetByIdAsync(1);
        Assert.NotNull(product);

        vm.AddProductCommand.Execute(product);

        Assert.Single(vm.Cart);
        Assert.Equal(product.Name, vm.Cart[0].ProductName);
        Assert.Equal(1m, vm.Cart[0].Quantity);
    }

    [Fact]
    public async Task QuantityCommands_ModifyLineQuantityAndRemoveWhenZero()
    {
        var vm = _fixture.CreateSalesViewModel();
        var product = await _fixture.ProductService.GetByIdAsync(1);
        vm.AddProductCommand.Execute(product);

        var line = vm.Cart[0];

        // Increase
        vm.IncreaseQuantityCommand.Execute(line);
        Assert.Equal(2m, line.Quantity);

        // Decrease back to 1
        vm.DecreaseQuantityCommand.Execute(line);
        Assert.Equal(1m, line.Quantity);

        // Decrease again removes line
        vm.DecreaseQuantityCommand.Execute(line);
        Assert.Empty(vm.Cart);
        Assert.True(vm.IsCartEmpty);
    }

    [Fact]
    public async Task LineDiscount_RecalculatesTotalsAccurately()
    {
        var vm = _fixture.CreateSalesViewModel();
        var product = await _fixture.ProductService.GetByIdAsync(1); // 27.00 LE, 4% VAT
        vm.AddProductCommand.Execute(product);

        var line = vm.Cart[0];
        // 10% discount on 27.00
        line.ApplyDiscount(10m);

        Assert.Equal(2.70m, line.DiscountAmount);
        Assert.True(vm.Totals.TotalAmount < 27m);
    }

    [Fact]
    public async Task ClearCart_WithConfirmation_EmptiesCart()
    {
        var vm = _fixture.CreateSalesViewModel();
        var product = await _fixture.ProductService.GetByIdAsync(1);
        vm.AddProductCommand.Execute(product);

        _fixture.Dialogs.ConfirmResult = true;
        vm.ClearCartCommand.Execute(null);

        Assert.Empty(vm.Cart);
        Assert.True(vm.IsCartEmpty);
    }

    [Fact]
    public async Task HoldAndResumeCart_ParksBasketAndRestoresAccurately()
    {
        var vm = _fixture.CreateSalesViewModel();
        var cashier = new UserDto { Id = 2, FullName = "محمود السيد", Role = UserRole.Cashier };
        await vm.InitializeAsync(cashier, "POS-01");
        vm.CurrentUserId = cashier.Id;

        // Open shift for cashier
        var shift = await _fixture.ShiftService.GetOrOpenAsync(new ShiftOpenRequestDto
        {
            UserId = cashier.Id,
            OpeningCash = 100m,
            TerminalId = "POS-01"
        });
        vm.ApplyOpenedShift(shift);

        // Add 2 items
        var product = await _fixture.ProductService.GetByIdAsync(1);
        vm.AddProductCommand.Execute(product);

        var totalBeforeHold = vm.Totals.TotalAmount;

        // Park basket
        _fixture.Dialogs.PromptResult = "عميل 1 - مستعجل";
        await vm.HoldCartCommand.ExecuteAsync(null);

        Assert.Empty(vm.Cart);
        Assert.Equal(1, vm.HeldCartCount);

        // Load held carts
        var heldList = await vm.LoadHeldCartsAsync();
        Assert.Single(heldList);

        // Resume basket
        var resumed = await vm.ResumeHeldCartAsync(heldList[0].Id);
        Assert.True(resumed);
        Assert.Single(vm.Cart);
        Assert.Equal(totalBeforeHold, vm.Totals.TotalAmount);
    }

    [Fact]
    public async Task Checkout_WhenNoShiftIsOpen_BlocksAndWarns()
    {
        var vm = _fixture.CreateSalesViewModel();
        var product = await _fixture.ProductService.GetByIdAsync(1);
        vm.AddProductCommand.Execute(product);

        vm.CheckoutCommand.Execute(null);

        Assert.Contains(_fixture.Dialogs.Warnings, w => w.Contains("ورديّة"));
    }

    [Fact]
    public async Task Checkout_WhenShiftOpenAndCartValid_CompletesSaleDirectlyAndClearsCart()
    {
        var vm = _fixture.CreateSalesViewModel();
        var cashier = new UserDto { Id = 2, FullName = "محمود السيد", Role = UserRole.Cashier };
        await vm.InitializeAsync(cashier, "POS-01");
        vm.CurrentUserId = cashier.Id;

        var shift = await _fixture.ShiftService.GetOrOpenAsync(new ShiftOpenRequestDto
        {
            UserId = 2,
            OpeningCash = 100m,
            TerminalId = "POS-01"
        });
        vm.ApplyOpenedShift(shift);

        var product = await _fixture.ProductService.GetByIdAsync(1);
        vm.AddProductCommand.Execute(product);

        SaleResultDto? completedSale = null;
        vm.SaleCompleted += (_, sale) => completedSale = sale;

        // Cash sale with change calculation (item is 27.00, paid 50.00 => change 23.00)
        vm.PaidAmount = 50m;
        await vm.CheckoutCommand.ExecuteAsync(null);

        Assert.NotNull(completedSale);
        Assert.Equal(27m, completedSale.TotalAmount);
        Assert.Equal(23m, completedSale.ChangeAmount);
        Assert.Empty(vm.Cart);
        Assert.Contains("باقي العميل", vm.LastChangeMessage);
    }

    [Fact]
    public async Task Checkout_WithWalletPayment_CompletesSaleForExactAmount()
    {
        var vm = _fixture.CreateSalesViewModel();
        var cashier = new UserDto { Id = 2, FullName = "محمود السيد", Role = UserRole.Cashier };
        await vm.InitializeAsync(cashier, "POS-01");
        vm.CurrentUserId = cashier.Id;

        var shift = await _fixture.ShiftService.GetOrOpenAsync(new ShiftOpenRequestDto
        {
            UserId = 2,
            OpeningCash = 100m,
            TerminalId = "POS-01"
        });
        vm.ApplyOpenedShift(shift);

        var product = await _fixture.ProductService.GetByIdAsync(1);
        vm.AddProductCommand.Execute(product);

        vm.SelectPaymentMethodCommand.Execute("Wallet");
        Assert.True(vm.IsWallet);

        SaleResultDto? completedSale = null;
        vm.SaleCompleted += (_, sale) => completedSale = sale;

        await vm.CheckoutCommand.ExecuteAsync(null);

        Assert.NotNull(completedSale);
        Assert.Equal(PaymentMethod.Wallet, completedSale.PaymentMethod);
        Assert.Empty(vm.Cart);
    }

    public void Dispose() => _fixture.Dispose();
}
