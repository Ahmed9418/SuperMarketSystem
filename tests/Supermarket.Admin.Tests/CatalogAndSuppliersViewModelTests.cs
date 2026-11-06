using Microsoft.EntityFrameworkCore;
using Supermarket.Admin.ViewModels;
using Xunit;

namespace Supermarket.Admin.Tests;

public sealed class CatalogAndSuppliersViewModelTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task CatalogViewModel_CategoryManagement_CreatesAndUpdatesCategory()
    {
        _fixture.SignInAsAdmin();
        var vm = new CatalogViewModel(_fixture.CategoryService, _fixture.UnitService, _fixture.Dialogs, _fixture.Session);

        await Task.Delay(100);

        Assert.NotEmpty(vm.Categories);

        // 1. Create a new category
        vm.NewCategoryCommand.Execute(null);
        vm.CategoryName = "حلويات وشوكولاتة";
        vm.CategoryDescription = "قسم الحلويات المستوردة والمحلية";
        vm.CategoryDisplayOrder = 10;

        await vm.SaveCategoryCommand.ExecuteAsync(null);

        var created = await _fixture.Database.Context.Categories.FirstOrDefaultAsync(c => c.Name == "حلويات وشوكولاتة");
        Assert.NotNull(created);
        Assert.Equal("قسم الحلويات المستوردة والمحلية", created!.Description);

        // 2. Edit existing category
        vm.SelectedCategory = vm.Categories.First(c => c.Id == created.Id);
        Assert.True(vm.IsEditingCategory);
        vm.CategoryName = "حلويات وسكاكر";
        await vm.SaveCategoryCommand.ExecuteAsync(null);

        var updated = await _fixture.Database.Context.Categories.FindAsync(created.Id);
        Assert.Equal("حلويات وسكاكر", updated!.Name);
    }

    [Fact]
    public async Task CatalogViewModel_UnitManagement_CreatesAndDeletesUnit()
    {
        _fixture.SignInAsAdmin();
        var vm = new CatalogViewModel(_fixture.CategoryService, _fixture.UnitService, _fixture.Dialogs, _fixture.Session);

        await Task.Delay(100);

        Assert.NotEmpty(vm.Units);

        // Create new unit
        vm.NewUnitCommand.Execute(null);
        vm.UnitName = "صندوق خشب";
        vm.UnitSymbol = "صندوق";
        vm.UnitAllowFractions = false;

        await vm.SaveUnitCommand.ExecuteAsync(null);

        var unit = await _fixture.Database.Context.Units.FirstOrDefaultAsync(u => u.Name == "صندوق خشب");
        Assert.NotNull(unit);
        Assert.Equal("صندوق", unit!.Symbol);

        // Delete unit (soft-deleted)
        var targetDto = vm.Units.First(u => u.Id == unit.Id);
        await vm.DeleteUnitCommand.ExecuteAsync(targetDto);
        var deleted = await _fixture.Database.Context.Units.FindAsync(unit.Id);
        Assert.NotNull(deleted);
        Assert.True(deleted!.IsDeleted);
    }

    [Fact]
    public async Task SuppliersViewModel_SearchAndToggleActive_WorksCorrectly()
    {
        _fixture.SignInAsAdmin();
        var vm = new SuppliersViewModel(_fixture.SupplierService, _fixture.Dialogs, _fixture.ServiceProvider, _fixture.Session);

        await Task.Delay(100);

        Assert.NotEmpty(vm.Suppliers);
        var firstSupplier = vm.Suppliers.First();
        var originalActive = firstSupplier.IsActive;

        // Toggle active
        await vm.ToggleActiveCommand.ExecuteAsync(firstSupplier);
        var updated = await _fixture.Database.Context.Suppliers.FindAsync(firstSupplier.Id);
        Assert.Equal(!originalActive, updated!.IsActive);
    }

    [Fact]
    public async Task SupplierEditViewModel_SaveNewSupplier_PersistsToDatabase()
    {
        _fixture.SignInAsAdmin();
        var vm = new SupplierEditViewModel(_fixture.SupplierService, _fixture.Dialogs);

        await vm.InitializeAsync(0);

        vm.SupplierName = "شركة النصر للتجارة";
        vm.ContactPerson = "أحمد خالد";
        vm.PhoneNumber = "01001234567";
        vm.TaxNumber = "123-456-789";

        var savedFired = false;
        vm.Saved += (_, _) => savedFired = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(savedFired);
        var supplier = await _fixture.Database.Context.Suppliers.FirstOrDefaultAsync(s => s.Name == "شركة النصر للتجارة");
        Assert.NotNull(supplier);
        Assert.Equal("أحمد خالد", supplier!.ContactPerson);
    }

    [Fact]
    public async Task SupplierEditViewModel_CreditAndDebitBalance_PersistCorrectly()
    {
        _fixture.SignInAsAdmin();

        // 1. Credit balance (رصيد علينا - موجب)
        var vmCredit = new SupplierEditViewModel(_fixture.SupplierService, _fixture.Dialogs);
        await vmCredit.InitializeAsync(0);
        vmCredit.SupplierName = "مورد رصيد علينا";
        vmCredit.CreditBalance = 5000m;
        await vmCredit.SaveCommand.ExecuteAsync(null);

        var creditSupplier = await _fixture.Database.Context.Suppliers.FirstAsync(s => s.Name == "مورد رصيد علينا");
        Assert.Equal(5000m, creditSupplier.Balance);

        // Edit and initialize existing credit supplier
        var vmEditCredit = new SupplierEditViewModel(_fixture.SupplierService, _fixture.Dialogs);
        await vmEditCredit.InitializeAsync(creditSupplier.Id);
        Assert.Equal(5000m, vmEditCredit.CreditBalance);
        Assert.Equal(0m, vmEditCredit.DebitBalance);

        // 2. Debit balance (رصيد لنا - سالب)
        var vmDebit = new SupplierEditViewModel(_fixture.SupplierService, _fixture.Dialogs);
        await vmDebit.InitializeAsync(0);
        vmDebit.SupplierName = "مورد رصيد لنا";
        vmDebit.DebitBalance = 1500m;
        await vmDebit.SaveCommand.ExecuteAsync(null);

        var debitSupplier = await _fixture.Database.Context.Suppliers.FirstAsync(s => s.Name == "مورد رصيد لنا");
        Assert.Equal(-1500m, debitSupplier.Balance);

        // Edit and initialize existing debit supplier
        var vmEditDebit = new SupplierEditViewModel(_fixture.SupplierService, _fixture.Dialogs);
        await vmEditDebit.InitializeAsync(debitSupplier.Id);
        Assert.Equal(1500m, vmEditDebit.DebitBalance);
        Assert.Equal(0m, vmEditDebit.CreditBalance);
    }
}
