using Microsoft.EntityFrameworkCore;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Common;
using Xunit;

namespace Supermarket.Admin.Tests;

public sealed class UsersAndPermissionsViewModelsTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task UsersViewModel_SearchAndRoleFiltering_FiltersCorrectly()
    {
        _fixture.SignInAsAdmin();
        var vm = new UsersViewModel(_fixture.UserAdmin, _fixture.ServiceProvider, _fixture.Dialogs, _fixture.Session);

        await Task.Delay(100);

        Assert.NotEmpty(vm.Users);

        // Filter by role: Cashier
        vm.RoleFilter = "كاشير";
        Assert.All(vm.FilteredUsers, u => Assert.Equal(UserRole.Cashier, u.Role));

        // Filter by keyword: "admin"
        vm.RoleFilter = "كل الأدوار";
        vm.SearchTerm = "admin";
        Assert.All(vm.FilteredUsers, u => Assert.Contains("admin", u.UserName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UsersViewModel_ToggleActive_UpdatesStatusInDatabase()
    {
        _fixture.SignInAsAdmin();
        var vm = new UsersViewModel(_fixture.UserAdmin, _fixture.ServiceProvider, _fixture.Dialogs, _fixture.Session);

        await Task.Delay(100);

        var cashier = vm.Users.First(u => u.UserName == "cashier");
        var originalStatus = cashier.IsActive;

        await vm.ToggleActiveCommand.ExecuteAsync(cashier);

        var updated = await _fixture.Database.Context.Users.FindAsync(cashier.Id);
        Assert.Equal(!originalStatus, updated!.IsActive);
    }

    [Fact]
    public async Task UsersViewModel_RestrictsNonAdminFromManagement()
    {
        _fixture.SignInAsCashier();
        var vm = new UsersViewModel(_fixture.UserAdmin, _fixture.ServiceProvider, _fixture.Dialogs, _fixture.Session);

        await Task.Delay(100);

        Assert.False(vm.CanManage);

        var adminUser = vm.Users.First(u => u.Role == UserRole.Administrator);
        await vm.ToggleActiveCommand.ExecuteAsync(adminUser);

        // Warning must be shown, operation rejected
        Assert.Contains(_fixture.Dialogs.Warnings, w => w.Contains("صلاحية"));
    }

    [Fact]
    public async Task PermissionsViewModel_Overrides_GrantRevokeAndRestoreDefaults()
    {
        _fixture.SignInAsAdmin();
        var cashier = await _fixture.Database.Context.Users.FirstAsync(u => u.Role == UserRole.Cashier);

        var vm = new PermissionsViewModel(_fixture.UserAdmin, _fixture.Dialogs, cashier.Id, _fixture.Session);

        await Task.Delay(100);

        Assert.NotEmpty(vm.Rows);
        Assert.Contains(cashier.FullName, vm.Title);

        // Select Products group
        vm.SelectedGroup = "المنتجات";
        var productRows = vm.FilteredRows.ToList();
        Assert.NotEmpty(productRows);

        // Cashiers don't have product edit by default
        var editRow = productRows.First(r => r.Key == AppPermissions.ProductsEdit);
        Assert.False(editRow.RoleDefault);
        Assert.False(editRow.IsGranted);
        Assert.False(editRow.IsOverride);
        Assert.Equal("افتراضي الدور", editRow.SourceText);

        // Grant ProductsEdit override
        editRow.IsGranted = true;
        Assert.True(editRow.IsOverride);
        Assert.Equal("مخصص", editRow.SourceText);

        // Save overrides
        var savedFired = false;
        vm.Saved += (_, _) => savedFired = true;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(savedFired);

        // Verify effective permission is now granted
        var effective = await _fixture.UserAdmin.GetEffectivePermissionsAsync(cashier.Id);
        Assert.Contains(AppPermissions.ProductsEdit, effective);

        // Now test RestoreDefaults
        vm.RestoreDefaultsCommand.Execute(null);
        Assert.False(editRow.IsGranted);
        Assert.False(editRow.IsOverride);
        await vm.SaveCommand.ExecuteAsync(null);

        var restoredEffective = await _fixture.UserAdmin.GetEffectivePermissionsAsync(cashier.Id);
        Assert.DoesNotContain(AppPermissions.ProductsEdit, restoredEffective);
    }
}

