using Supermarket.Admin.Services;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Xunit;

namespace Supermarket.Admin.Tests;

public sealed class AuthAndSessionTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void AuthSession_SignIn_SetsPropertiesAndDefaults()
    {
        var session = _fixture.Session;
        var user = new UserDto
        {
            Id = 42,
            UserName = "stockkeeper",
            FullName = "أمين المخزن",
            Role = UserRole.StockKeeper,
            IsActive = true
        };

        session.SignIn(user);

        Assert.True(session.IsAuthenticated);
        Assert.False(session.IsAdministrator);
        Assert.Equal("أمين المخزن", session.DisplayName);
        Assert.Equal("أمين مخزن", session.RoleNameArabic);

        // StockKeeper defaults should include StockView, ProductsView, but not UsersView
        Assert.True(session.Can(AppPermissions.StockView));
        Assert.True(session.Can(AppPermissions.ProductsView));
        Assert.False(session.Can(AppPermissions.UsersView));
        Assert.False(session.Can(AppPermissions.SettingsEdit));
    }

    [Fact]
    public void AuthSession_Administrator_HoldsAllPermissionsEvenIfMissing()
    {
        var session = _fixture.Session;
        var admin = new UserDto
        {
            Id = 1,
            UserName = "admin",
            FullName = "مدير النظام",
            Role = UserRole.Administrator,
            IsActive = true
        };

        session.SignIn(admin);

        Assert.True(session.IsAdministrator);
        Assert.True(session.Can(AppPermissions.UsersView));
        Assert.True(session.Can(AppPermissions.SettingsEdit));
        Assert.True(session.Can("NonExistentPermission")); // Admin bypass
    }

    [Fact]
    public async Task AuthSession_LoadPermissions_AppliesOverridesFromDatabase()
    {
        var session = _fixture.Session;
        _fixture.SignInAsCashier();

        // Cashier normally does NOT have ProductsEdit
        Assert.False(session.Can(AppPermissions.ProductsEdit));

        // Grant ProductsEdit to this cashier in DB
        var user = session.CurrentUser!;
        var permissions = await _fixture.UserAdmin.GetPermissionsAsync(user.Id);
        Assert.NotNull(permissions);
        var targetRow = permissions!.Rows.First(r => r.Key == AppPermissions.ProductsEdit);
        targetRow.IsGranted = true;
        await _fixture.UserAdmin.SavePermissionsAsync(permissions);

        // Reload permissions into session
        await session.LoadPermissionsAsync(_fixture.UserAdmin);

        Assert.True(session.Can(AppPermissions.ProductsEdit));
    }

    [Fact]
    public void AuthSession_SignOut_ClearsUserAndPermissions()
    {
        var session = _fixture.Session;
        _fixture.SignInAsAdmin();
        Assert.True(session.IsAuthenticated);

        session.SignOut();

        Assert.False(session.IsAuthenticated);
        Assert.Null(session.CurrentUser);
        Assert.Empty(session.Permissions);
        Assert.False(session.Can(AppPermissions.ProductsView));
    }

    [Fact]
    public async Task LoginViewModel_FailedSignIn_SetsErrorMessage()
    {
        var vm = new LoginViewModel(_fixture.AuthService, _fixture.UserAdmin, _fixture.Session, _fixture.Dialogs)
        {
            UserName = "admin"
        };

        var signedInFired = false;
        vm.SignedIn += (_, _) => signedInFired = true;

        await vm.SignInCommand.ExecuteAsync("WrongPassword123");

        Assert.False(signedInFired);
        Assert.False(_fixture.Session.IsAuthenticated);
        Assert.NotEmpty(vm.ErrorMessage);
    }

    [Fact]
    public async Task LoginViewModel_SuccessfulSignIn_RaisesEventAndPopulatesSession()
    {
        var vm = new LoginViewModel(_fixture.AuthService, _fixture.UserAdmin, _fixture.Session, _fixture.Dialogs)
        {
            UserName = "admin"
        };

        var signedInFired = false;
        vm.SignedIn += (_, _) => signedInFired = true;

        await vm.SignInCommand.ExecuteAsync("Admin@123");

        Assert.True(signedInFired);
        Assert.True(_fixture.Session.IsAuthenticated);
        Assert.Equal("admin", _fixture.Session.CurrentUser?.UserName);
        Assert.True(_fixture.Session.IsAdministrator);
    }
}

