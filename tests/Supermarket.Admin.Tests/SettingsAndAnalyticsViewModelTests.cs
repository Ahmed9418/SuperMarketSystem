using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.Admin.Services;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Common;
using Supermarket.Core.DependencyInjection;
using Xunit;

namespace Supermarket.Admin.Tests;

public sealed class SettingsAndAnalyticsViewModelTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task SettingsViewModel_LoadAndSave_HandlesVatRatePercentageCorrectly()
    {
        _fixture.SignInAsAdmin();
        var vm = new SettingsViewModel(_fixture.StoreSettings, _fixture.Reports, _fixture.Dialogs, _fixture.Session);

        await Task.Delay(100);

        // Seeded DefaultVatRate is 0.14m; UI must display 14m percent
        Assert.Equal(14m, vm.DefaultVatRate);
        Assert.Equal("سوبر ماركت الأمانة", vm.StoreName);

        // Change VAT rate to 15% and save
        vm.DefaultVatRate = 15m;
        vm.StoreName = "سوبر ماركت النور";
        await vm.SaveCommand.ExecuteAsync(null);

        // StoreSetting in database must now store 0.15m fraction
        var settings = await _fixture.StoreSettings.GetAsync();
        Assert.Equal(0.15m, settings.DefaultVatRate);
        Assert.Equal("سوبر ماركت النور", settings.StoreName);
    }

    [Fact]
    public async Task SettingsViewModel_Validation_PreventsEmptyStoreNameAndInvalidVatRate()
    {
        _fixture.SignInAsAdmin();
        var vm = new SettingsViewModel(_fixture.StoreSettings, _fixture.Reports, _fixture.Dialogs, _fixture.Session);

        await Task.Delay(100);

        // 1. Empty store name
        vm.StoreName = "   ";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains(_fixture.Dialogs.Warnings, w => w.Contains("اسم المتجر"));

        // 2. VAT rate out of range (e.g. > 100)
        vm.StoreName = "سوبر ماركت";
        vm.DefaultVatRate = 150m;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains(_fixture.Dialogs.Warnings, w => w.Contains("نسبة الضريبة"));
    }

    [Fact]
    public void SettingsViewModel_LineWidthHint_ProvidesHumanReadableGuidance()
    {
        _fixture.SignInAsAdmin();
        var vm = new SettingsViewModel(_fixture.StoreSettings, _fixture.Reports, _fixture.Dialogs, _fixture.Session);

        vm.ReceiptLineWidth = 32;
        Assert.Contains("58 مم", vm.LineWidthHint);

        vm.ReceiptLineWidth = 42;
        Assert.Contains("80 مم", vm.LineWidthHint);
    }

    [Fact]
    public async Task SettingsViewModel_PaperSizeAndLogo_SaveAndLoadProperly()
    {
        _fixture.SignInAsAdmin();
        var vm = new SettingsViewModel(_fixture.StoreSettings, _fixture.Reports, _fixture.Dialogs, _fixture.Session);

        await Task.Delay(100);

        Assert.Equal("80mm", vm.InvoicePaperSize);
        Assert.True(vm.IsThermalSelected);

        vm.InvoicePaperSize = "A4";
        Assert.False(vm.IsThermalSelected);
        Assert.Equal("A4", vm.SelectedPaperSizeOption?.Key);

        vm.LogoPath = "C:\\fake\\logo.png";
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = await _fixture.StoreSettings.GetAsync();
        Assert.Equal("A4", saved.InvoicePaperSize);
        Assert.Equal("C:\\fake\\logo.png", saved.LogoPath);
    }

    [Fact]
    public async Task SalesAnalyticsViewModel_PresetsAndDerivedText_ComputeAccurately()
    {
        _fixture.SignInAsAdmin();
        var vm = new SalesAnalyticsViewModel(
            _fixture.Reports,
            _fixture.UserAdmin,
            _fixture.StoreSettings,
            _fixture.Dialogs,
            _fixture.Session);

        await Task.Delay(100);

        // Test Today preset
        await vm.ApplyPresetCommand.ExecuteAsync("اليوم");
        Assert.Equal(DateTime.Today, vm.FromDate);
        Assert.Equal(DateTime.Today, vm.ToDate);
        Assert.Equal($"{DateTime.Today:dd/MM/yyyy} — {DateTime.Today:dd/MM/yyyy}", vm.PeriodText);

        // Test Last 7 Days preset
        await vm.ApplyPresetCommand.ExecuteAsync("آخر 7 أيام");
        Assert.Equal(DateTime.Today.AddDays(-6), vm.FromDate);
        Assert.Equal(DateTime.Today, vm.ToDate);

        // Verify chart series arrays are populated
        Assert.NotNull(vm.SalesSeries);
        Assert.NotNull(vm.PaymentSeries);
        Assert.NotNull(vm.CategorySeries);
        Assert.NotNull(vm.TopProductSeries);
    }

    [Fact]
    public void DI_Container_Configuration_ResolvesAllAdminViewModelsAndServices()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = "DataSource=:memory:",
                ["Application:Name"] = "Supermarket.Admin",
                ["Application:TerminalId"] = "ADMIN-PC"
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddSupermarketCore(config, "Admin");

        // Admin registrations
        services.AddSingleton<IDialogService, MockDialogService>();
        services.AddSingleton<AuthSession>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<MainViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<LowStockViewModel>();
        services.AddTransient<StockMovementsViewModel>();
        services.AddTransient<CatalogViewModel>();
        services.AddTransient<SalesAnalyticsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<SettingsViewModel>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Resolve each service and viewModel
        Assert.NotNull(provider.GetRequiredService<AuthSession>());
        Assert.NotNull(provider.GetRequiredService<NavigationService>());
        Assert.NotNull(provider.GetRequiredService<MainViewModel>());
        Assert.NotNull(provider.GetRequiredService<LoginViewModel>());
        Assert.NotNull(provider.GetRequiredService<DashboardViewModel>());
        Assert.NotNull(provider.GetRequiredService<ProductsViewModel>());
        Assert.NotNull(provider.GetRequiredService<SuppliersViewModel>());
        Assert.NotNull(provider.GetRequiredService<LowStockViewModel>());
        Assert.NotNull(provider.GetRequiredService<StockMovementsViewModel>());
        Assert.NotNull(provider.GetRequiredService<CatalogViewModel>());
        Assert.NotNull(provider.GetRequiredService<SalesAnalyticsViewModel>());
        Assert.NotNull(provider.GetRequiredService<UsersViewModel>());
        Assert.NotNull(provider.GetRequiredService<SettingsViewModel>());
    }
}

