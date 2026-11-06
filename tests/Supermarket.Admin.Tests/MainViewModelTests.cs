using Microsoft.Extensions.DependencyInjection;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Common;
using Xunit;

namespace Supermarket.Admin.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void MainViewModel_InitialState_NavigatesToDashboard()
    {
        _fixture.SignInAsAdmin();
        var vm = _fixture.ServiceProvider.GetRequiredService<MainViewModel>();

        vm.Initialize();

        Assert.IsType<DashboardViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("لوحة التحكم", vm.CurrentPageTitle);
    }

    [Fact]
    public void MainViewModel_NavigationCommands_SwitchActiveViewModelAndTitle()
    {
        _fixture.SignInAsAdmin();
        var vm = _fixture.ServiceProvider.GetRequiredService<MainViewModel>();

        vm.NavigateToProductsCommand.Execute(null);
        Assert.IsType<ProductsViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("إدارة المنتجات", vm.CurrentPageTitle);

        vm.NavigateToSuppliersCommand.Execute(null);
        Assert.IsType<SuppliersViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("إدارة الموردين", vm.CurrentPageTitle);

        vm.NavigateToStockMovementsCommand.Execute(null);
        Assert.IsType<StockMovementsViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("حركة المخزون", vm.CurrentPageTitle);

        vm.NavigateToCatalogCommand.Execute(null);
        Assert.IsType<CatalogViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("الأقسام ووحدات القياس", vm.CurrentPageTitle);

        vm.NavigateToLowStockCommand.Execute(null);
        Assert.IsType<LowStockViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("نواقص المخزون", vm.CurrentPageTitle);

        vm.NavigateToAnalyticsCommand.Execute(null);
        Assert.IsType<SalesAnalyticsViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("المبيعات والتحليلات", vm.CurrentPageTitle);

        vm.NavigateToUsersCommand.Execute(null);
        Assert.IsType<UsersViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("المستخدمون والصلاحيات", vm.CurrentPageTitle);

        vm.NavigateToSettingsCommand.Execute(null);
        Assert.IsType<SettingsViewModel>(_fixture.Navigation.CurrentViewModel);
        Assert.Equal("إعدادات المتجر", vm.CurrentPageTitle);
    }

    [Fact]
    public void MainViewModel_Administrator_HasAllModulePermissionsEnabled()
    {
        _fixture.SignInAsAdmin();
        var vm = _fixture.ServiceProvider.GetRequiredService<MainViewModel>();

        Assert.True(vm.CanViewReports);
        Assert.True(vm.CanViewUsers);
        Assert.True(vm.CanViewSettings);
        Assert.True(vm.CanViewProducts);
        Assert.True(vm.CanViewStock);
        Assert.True(vm.CanViewCatalog);
    }

    [Fact]
    public void MainViewModel_Cashier_RestrictsAdministrativeModules()
    {
        _fixture.SignInAsCashier();
        var vm = _fixture.ServiceProvider.GetRequiredService<MainViewModel>();

        Assert.False(vm.CanViewUsers);
        Assert.False(vm.CanViewSettings);
        Assert.False(vm.CanViewCatalog);
    }
}

