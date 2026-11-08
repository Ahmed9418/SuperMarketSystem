using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.POS.ViewModels;
using Xunit;

namespace Supermarket.POS.Tests;

public sealed class PaymentAndShiftTests : IDisposable
{
    private readonly PosTestFixture _fixture = new();

    [Fact]
    public async Task Payment_CashExactAmount_CanConfirmIsTrue()
    {
        var vm = _fixture.CreatePaymentViewModel();
        var shift = await _fixture.ShiftService.GetOrOpenAsync(new ShiftOpenRequestDto
        {
            UserId = 2,
            OpeningCash = 100m,
            TerminalId = "POS-01"
        });

        vm.UserId = 2;
        vm.ShiftId = shift.Id;

        var lines = new List<CartLineDto>
        {
            new()
            {
                ProductId = 1,
                ProductName = "لبن جهينة كامل الدسم 1 لتر",
                Quantity = 1,
                UnitPrice = 27m,
                UnitCost = 22.5m,
                VatRate = 4m
            }
        };

        var totals = new CartPricingDto
        {
            SubTotal = 27m,
            TotalAmount = 27m,
            TaxAmount = 1.04m,
            LineCount = 1,
            TotalQuantity = 1m
        };

        await vm.InitializeAsync(lines, totals);

        Assert.True(vm.IsCash);
        Assert.Equal(27m, vm.PaidAmount);
        Assert.Equal(0m, vm.ChangeAmount);
        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public async Task Payment_CashOverpayment_CalculatesChangeAccurately()
    {
        var vm = _fixture.CreatePaymentViewModel();
        var lines = new List<CartLineDto>();
        var totals = new CartPricingDto
        {
            SubTotal = 75m,
            TotalAmount = 75m,
            LineCount = 1,
            TotalQuantity = 1m
        };

        await vm.InitializeAsync(lines, totals);
        vm.PaidAmount = 100m;

        Assert.Equal(25m, vm.ChangeAmount);
        Assert.Equal(0m, vm.RemainingAmount);
        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public async Task Payment_Underpayment_DisablesConfirm()
    {
        var vm = _fixture.CreatePaymentViewModel();
        var lines = new List<CartLineDto>();
        var totals = new CartPricingDto
        {
            SubTotal = 100m,
            TotalAmount = 100m,
            LineCount = 1,
            TotalQuantity = 1m
        };

        await vm.InitializeAsync(lines, totals);
        vm.PaidAmount = 50m;

        Assert.Equal(50m, vm.RemainingAmount);
        Assert.Equal(0m, vm.ChangeAmount);
        Assert.False(vm.CanConfirm);
    }

    [Fact]
    public async Task ShiftClose_CalculatesVarianceAccurately()
    {
        // User 3 (Manager) has no open shift initially
        var shift = await _fixture.ShiftService.GetOrOpenAsync(new ShiftOpenRequestDto
        {
            UserId = 3,
            OpeningCash = 100m,
            TerminalId = "POS-02"
        });

        var vm = _fixture.CreateShiftCloseViewModel();
        await vm.InitializeAsync(shift.Id, isShiftClosed: false);

        // Expected cash is 100
        Assert.Equal(100m, vm.ExpectedCash);

        // Cashier enters 120 (surplus of 20)
        vm.ClosingCash = 120m;
        Assert.Equal(20m, vm.Difference);
        Assert.Equal("زيادة في النقدية", vm.DifferenceVerdict);

        // Cashier enters 80 (shortage of 20)
        vm.ClosingCash = 80m;
        Assert.Equal(-20m, vm.Difference);
        Assert.Equal("عجز في النقدية", vm.DifferenceVerdict);
    }

    public void Dispose() => _fixture.Dispose();
}
