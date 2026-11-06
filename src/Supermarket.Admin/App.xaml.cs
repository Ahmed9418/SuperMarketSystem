using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supermarket.Admin.Services;
using Supermarket.Admin.ViewModels;
using Supermarket.Admin.Views;
using Supermarket.Core.DependencyInjection;
using Supermarket.Wpf;

namespace Supermarket.Admin;

/// <summary>
/// Composition root of the admin application: Arabic RTL culture, DI container, global error handling
/// and the sign-in window.
/// </summary>
public partial class App : Application
{
    /// <summary>Service provider resolved once at start-up.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Configuration loaded from appsettings.json.</summary>
    public static IConfiguration Configuration { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Arabic culture + Right-to-Left language metadata BEFORE the first window exists, so that
        // dates, numbers and every element created afterwards are laid out correctly.
        ArabicLayout.Configure();

        // Also pin the Arabic window style to every window type of this assembly (see
        // ArabicLayout.PinWindowStyle: an implicit Window style never reaches a derived window).
        ArabicLayout.PinWindowStyle(this, typeof(App).Assembly);

        // appsettings.json is the shipped default; appsettings.Local.json (git-ignored) and the
        // environment variables let a machine override the SQL instance without editing the file.
        Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("SUPERMARKET_")
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services);

        // ValidateScopes / ValidateOnBuild turn a missing registration into a clear start-up error
        // instead of a cryptic "Unable to resolve service" the first time a window opens.
        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // One place for unhandled exceptions: show an Arabic message and keep the shell alive.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ArabicMessageBox.ShowError(
                args.ExceptionObject?.ToString() ?? "خطأ غير معروف",
                "خطأ غير متوقع");

        base.OnStartup(e);

        var login = Services.GetRequiredService<LoginWindow>();
        MainWindow = login;
        login.Show();
    }

    /// <summary>Registers the shared core services and the admin-specific windows / view models.</summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(Configuration);

        // Shared library: database factory, business services, hasher, current-user holder.
        services.AddSupermarketCore(Configuration, "Admin");

        // UI services.
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<AuthSession>();
        services.AddSingleton<NavigationService>();

        // Shell.
        services.AddSingleton<LoginWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();

        // One view model per page.
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
        services.AddTransient<ProductEditViewModel>();
        services.AddTransient<SupplierEditViewModel>();
        services.AddTransient<StockAdjustmentViewModel>();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Services?.GetService<ILogger<App>>()?.LogError(e.Exception, "Unhandled exception in the admin application");

        ArabicMessageBox.ShowError(
            $"حدث خطأ غير متوقع:\n{e.Exception.Message}\n\nيمكنك متابعة العمل، وإذا تكرر الخطأ برجاء إبلاغ الدعم الفني.",
            "خطأ");

        e.Handled = true;
    }
}
