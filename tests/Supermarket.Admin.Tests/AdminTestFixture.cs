using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Admin.Services;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DependencyInjection;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Supermarket.Core.Tests;

namespace Supermarket.Admin.Tests;

public sealed class AdminTestFixture : IDisposable
{
    public SqliteTestDatabase Database { get; }
    public IServiceProvider ServiceProvider { get; }
    public AuthSession Session { get; }
    public MockDialogService Dialogs { get; }
    public NavigationService Navigation { get; }
    public MutableCurrentUserService CurrentUser { get; }
    public IProductService ProductService { get; }
    public ICatalogService CatalogService { get; }
    public ICategoryService CategoryService { get; }
    public IUnitService UnitService { get; }
    public ISupplierService SupplierService { get; }
    public IInventoryService InventoryService { get; }
    public IStockService StockService { get; }
    public IStoreSettingsService StoreSettings { get; }
    public IUserAdminService UserAdmin { get; }
    public IReportService Reports { get; }
    public IDashboardService Dashboard { get; }
    public IAuthService AuthService { get; }

    public AdminTestFixture()
    {
        Database = new SqliteTestDatabase();
        Dialogs = new MockDialogService();
        Navigation = new NavigationService();
        CurrentUser = new MutableCurrentUserService();
        Session = new AuthSession(CurrentUser);

        var contextFactory = new TestContextFactory(Database);
        var clock = new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
        var hasher = new PasswordHasher<User>();

        StockService = new StockService(Database.Context, NullLogger<StockService>.Instance);
        StoreSettings = new StoreSettingsService(contextFactory);
        ProductService = new ProductService(Database.Context, StockService, StoreSettings, NullLogger<ProductService>.Instance);
        CategoryService = new CategoryService(Database.Context, NullLogger<CategoryService>.Instance);
        UnitService = new UnitService(Database.Context, NullLogger<UnitService>.Instance);
        SupplierService = new SupplierService(Database.Context, NullLogger<SupplierService>.Instance);
        CatalogService = new CatalogService(Database.Context, NullLogger<CatalogService>.Instance);
        InventoryService = new InventoryService(Database.Context, StockService, NullLogger<InventoryService>.Instance);
        UserAdmin = new UserAdminService(Database.Context, hasher, CurrentUser, clock, NullLogger<UserAdminService>.Instance);
        Reports = new ReportService(Database.Context, clock);
        Dashboard = new DashboardService(Database.Context, clock);
        AuthService = new AuthService(Database.Context, hasher, clock, NullLogger<AuthService>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IDialogService>(Dialogs);
        services.AddSingleton(Session);
        services.AddSingleton(Navigation);
        services.AddSingleton(CurrentUser);
        services.AddSingleton(ProductService);
        services.AddSingleton(CatalogService);
        services.AddSingleton(CategoryService);
        services.AddSingleton(UnitService);
        services.AddSingleton(SupplierService);
        services.AddSingleton(InventoryService);
        services.AddSingleton(StockService);
        services.AddSingleton(StoreSettings);
        services.AddSingleton(UserAdmin);
        services.AddSingleton(Reports);
        services.AddSingleton(Dashboard);
        services.AddSingleton(AuthService);

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<ProductEditViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<SupplierEditViewModel>();
        services.AddTransient<StockMovementsViewModel>();
        services.AddTransient<StockAdjustmentViewModel>();
        services.AddTransient<CatalogViewModel>();
        services.AddTransient<LowStockViewModel>();
        services.AddTransient<SalesAnalyticsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<SettingsViewModel>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public void SignInAsAdmin()
    {
        var admin = Database.Context.Users.First(u => u.Role == UserRole.Administrator);
        Session.SignIn(new Core.DTOs.UserDto
        {
            Id = admin.Id,
            UserName = admin.UserName,
            FullName = admin.FullName,
            Role = admin.Role,
            IsActive = admin.IsActive
        });
    }

    public void SignInAsCashier()
    {
        var cashier = Database.Context.Users.First(u => u.Role == UserRole.Cashier);
        Session.SignIn(new Core.DTOs.UserDto
        {
            Id = cashier.Id,
            UserName = cashier.UserName,
            FullName = cashier.FullName,
            Role = cashier.Role,
            IsActive = cashier.IsActive
        });
    }

    public void Dispose()
    {
        Database.Dispose();
    }
}

