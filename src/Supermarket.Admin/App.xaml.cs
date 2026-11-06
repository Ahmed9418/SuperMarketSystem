using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supermarket.Admin.Services;
using Supermarket.Admin.ViewModels;
using Supermarket.Admin.Views;
using Supermarket.Core.DependencyInjection;

namespace Supermarket.Admin;

/// <summary>
/// Composition root of the admin application. Builds the DI container, forces the whole
/// process into the Arabic (Egypt) culture with an RTL flow direction and shows the shell.
/// </summary>
public partial class App : Application
{
    /// <summary>Service provider resolved once at start-up.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Configuration loaded from appsettings.json.</summary>
    public static IConfiguration Configuration { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Arabic culture before any window is created so that dates, numbers and the
        // RightToLeft layout behave correctly everywhere.
        var culture = new CultureInfo("UTF-8");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        // Every WPF element inherits RightToLeft from here, including dialogs created in code.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services);
        // ValidateScopes / ValidateOnBuild make a missing registration fail here, at start-up, with the
        // exact type name — instead of a cryptic "Unable to resolve service" the first time a window opens.
        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Centralised unhandled exception handling: a crash shows an Arabic message instead of
        // the default .NET dialog, and the application stays alive where possible.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            MessageBox.Show(args.ExceptionObject?.ToString() ?? "خطأ غير معروف",
                "خطأ غير متوقع", MessageBoxButton.OK, MessageBoxImage.Error);

        base.OnStartup(e);

        var login = Services.GetRequiredService<LoginWindow>();
        MainWindow = login;
        login.Show();
    }

    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(Configuration);

        // Shared library: DbContext factory, services, hasher, current user holder.
        services.AddSupermarketCore(Configuration, "Admin");

        // UI services.
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<AuthSession>();
        services.AddSingleton<NavigationService>();

        // Windows.
        services.AddSingleton<LoginWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();

        // View models (transient screens).
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
    }


    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = Services?.GetService<ILogger<App>>();
        logger?.LogError(e.Exception, "Unhandled exception in the admin application");

        MessageBox.Show(
            $"حدث خطأ غير متوقع:\n{e.Exception.Message}\n\nيمكنك متابعة العمل، وإذا تكرر الخطأ برجاء إبلاغ الدعم الفني.",
            "خطأ",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
