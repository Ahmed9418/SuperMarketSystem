using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>Phase 5: user administration, the permission matrix and the guard rails.</summary>
public sealed class UserAdminServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database;
    private readonly FixedClock _clock = new(new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc));
    private readonly PasswordHasher<User> _hasher = new();
    private readonly TestUserHolder _currentUser = new() { UserName = "admin", UserId = 1 };

    public UserAdminServiceTests() => _database = new SqliteTestDatabase();

    private UserAdminService CreateService() =>
        new(_database.Context, _hasher, _currentUser, _clock, TestServices.Logger<UserAdminService>());

    private static UserEditDto NewUser(string userName, UserRole role = UserRole.Cashier) => new()
    {
        UserName = userName,
        FullName = $"مستخدم {userName}",
        Role = role,
        Password = "Secret@123",
        Pin = "1234",
        IsActive = true
    };

    [Fact]
    public async Task CreateAsync_HashesTheSecretsAndAcceptsTheLogin()
    {
        var service = CreateService();

        var created = await service.CreateAsync(NewUser("newcashier"));

        Assert.True(created.Succeeded);

        var user = await _database.Context.Users.SingleAsync(u => u.UserName == "newcashier");
        Assert.NotEqual("Secret@123", user.PasswordHash);                       // never stored in clear
        Assert.NotEqual("1234", user.PinHash);

        var passwordCheck = _hasher.VerifyHashedPassword(user, user.PasswordHash, "Secret@123");
        Assert.NotEqual(PasswordVerificationResult.Failed, passwordCheck);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicatesWeakSecretsAndBadPins()
    {
        var service = CreateService();

        Assert.True((await service.CreateAsync(NewUser("secondcashier"))).Succeeded);
        var duplicate = await service.CreateAsync(NewUser("secondcashier"));
        Assert.False(duplicate.Succeeded);
        Assert.Contains("مستخدم بالفعل", duplicate.ErrorMessage);

        var weak = NewUser("weakuser");
        weak.Password = "123";
        Assert.False((await service.CreateAsync(weak)).Succeeded);

        var badPin = NewUser("badpin");
        badPin.Pin = "12a4";
        Assert.False((await service.CreateAsync(badPin)).Succeeded);
    }

    [Fact]
    public async Task UpdateAsync_KeepsSecretsWhenFieldsAreEmpty()
    {
        var service = CreateService();
        var before = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.UserName == "cashier");

        var dto = await service.GetForEditAsync(before.Id);
        Assert.NotNull(dto);
        dto!.FullName = "محمود السيد المعدل";
        dto.Password = null;
        dto.Pin = null;

        Assert.True((await service.UpdateAsync(dto)).Succeeded);

        var after = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.Id == before.Id);
        Assert.Equal("محمود السيد المعدل", after.FullName);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.PinHash, after.PinHash);
    }

    [Fact]
    public async Task UpdateAsync_ProtectsTheLastAdministrator()
    {
        var service = CreateService();

        // The seed contains exactly one administrator (id 1) and the operator is that user.
        var admin = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.Role == UserRole.Administrator);

        var dto = await service.GetForEditAsync(admin.Id);
        dto!.Role = UserRole.Manager;

        var result = await service.UpdateAsync(dto);

        Assert.False(result.Succeeded);
        Assert.Contains("مدير نظام", result.ErrorMessage);

        var stillAdmin = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id);
        Assert.Equal(UserRole.Administrator, stillAdmin.Role);
    }

    [Fact]
    public async Task SetActiveAsync_NeverSuspendsTheCurrentOperator()
    {
        var service = CreateService();

        var result = await service.SetActiveAsync(_currentUser.UserId!.Value, false);

        Assert.False(result.Succeeded);
        Assert.True((await _database.Context.Users.AsNoTracking().SingleAsync(u => u.Id == 1)).IsActive);
    }

    [Fact]
    public async Task GetPermissions_ReturnsTheRoleDefaultsPlusTheOverrides()
    {
        var service = CreateService();
        var cashier = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.UserName == "cashier");

        var permissions = await service.GetPermissionsAsync(cashier.Id);

        Assert.NotNull(permissions);
        Assert.Equal(UserRole.Cashier, permissions!.Role);
        Assert.Equal(AppPermissions.Catalog.Count, permissions.Rows.Count);

        // Cashier default: POS sell + shift close, no product editing.
        Assert.True(permissions.Rows.Single(r => r.Key == AppPermissions.PosSell).IsGranted);
        Assert.False(permissions.Rows.Single(r => r.Key == AppPermissions.ProductsEdit).IsGranted);
        Assert.False(permissions.Rows.Single(r => r.Key == AppPermissions.ProductsEdit).IsOverride);
    }

    [Fact]
    public async Task SavePermissions_StoresOnlyTheOverridesAndResolvesThem()
    {
        var service = CreateService();
        var cashier = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.UserName == "cashier");

        var matrix = (await service.GetPermissionsAsync(cashier.Id))!;

        // Grant Products.Edit (not a cashier default) and revoke Invoices.View (a cashier default).
        matrix.Rows.Single(r => r.Key == AppPermissions.ProductsEdit).IsGranted = true;
        matrix.Rows.Single(r => r.Key == AppPermissions.InvoicesView).IsGranted = false;

        Assert.True((await service.SavePermissionsAsync(matrix)).Succeeded);

        var stored = await _database.Context.UserPermissions
            .AsNoTracking()
            .Where(p => p.UserId == cashier.Id)
            .ToListAsync();

        // Only the two deviations are persisted: the rest follows the role.
        Assert.Equal(2, stored.Count);
        Assert.True(stored.Single(p => p.PermissionKey == AppPermissions.ProductsEdit).IsGranted);
        Assert.False(stored.Single(p => p.PermissionKey == AppPermissions.InvoicesView).IsGranted);

        var effective = await service.GetEffectivePermissionsAsync(cashier.Id);
        Assert.Contains(AppPermissions.ProductsEdit, effective);
        Assert.DoesNotContain(AppPermissions.InvoicesView, effective);
        Assert.Contains(AppPermissions.PosSell, effective);

        // Setting a value back to the role default removes the override row.
        matrix.Rows.Single(r => r.Key == AppPermissions.ProductsEdit).IsGranted = false;
        Assert.True((await service.SavePermissionsAsync(matrix)).Succeeded);
        Assert.Equal(1, await _database.Context.UserPermissions.CountAsync(p => p.UserId == cashier.Id));
    }

    [Fact]
    public async Task SavePermissions_RefusesToLockTheOperatorOutOfAdministration()
    {
        var service = CreateService();
        var admin = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.Id == _currentUser.UserId!.Value);

        var matrix = (await service.GetPermissionsAsync(admin.Id))!;
        matrix.Rows.Single(r => r.Key == AppPermissions.UsersManage).IsGranted = false;

        var result = await service.SavePermissionsAsync(matrix);

        Assert.False(result.Succeeded);
        Assert.Contains("إدارة المستخدمين", result.ErrorMessage);
    }

    [Fact]
    public async Task GetAllAsync_ReportsTheEffectivePermissionCount()
    {
        var service = CreateService();
        var users = await service.GetAllAsync();

        Assert.Equal(4, users.Count);

        var admin = users.Single(u => u.Role == UserRole.Administrator);
        Assert.Equal(AppPermissions.Catalog.Count, admin.GrantedPermissionCount);

        var cashier = users.Single(u => u.UserName == "cashier");
        Assert.Equal(AppPermissions.DefaultsFor(UserRole.Cashier).Count, cashier.GrantedPermissionCount);
        Assert.False(cashier.HasCustomPermissions);
    }

    [Fact]
    public async Task ResetPasswordAndPin_ReplaceTheSecrets()
    {
        var service = CreateService();
        var cashier = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.UserName == "cashier");

        Assert.True((await service.ResetPasswordAsync(cashier.Id, "NewPass@123")).Succeeded);
        Assert.True((await service.ResetPinAsync(cashier.Id, "9876")).Succeeded);

        var updated = await _database.Context.Users.AsNoTracking().SingleAsync(u => u.Id == cashier.Id);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(updated, updated.PasswordHash, "NewPass@123"));
        Assert.NotEqual(PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(updated, updated.PinHash!, "9876"));

        Assert.False((await service.ResetPinAsync(cashier.Id, "12")).Succeeded);
        Assert.False((await service.ResetPasswordAsync(cashier.Id, "123")).Succeeded);
    }

    public void Dispose() => _database.Dispose();

    /// <summary>Mutable current user used to act as the signed-in administrator.</summary>
    private sealed class TestUserHolder : ICurrentUserService
    {
        public string? UserName { get; set; }

        public int? UserId { get; set; }
    }
}
