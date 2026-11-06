using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supermarket.Core.DependencyInjection;
using Supermarket.Core.Interfaces;
using Supermarket.POS.Printing;
using Supermarket.POS.Services;
using Supermarket.POS.ViewModels;
using Supermarket.POS.Views;
using Supermarket.Wpf;

namespace Supermarket.POS;

/// <summary>
/// Composition root of the point of sale terminal: Arabic RTL culture, DI container, a database
/// readiness check and the cashier sign-in window.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static IConfiguration Configuration { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Culture + RTL metadata first: a scanner keystroke can arrive milliseconds after the window.
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

        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        base.OnStartup(e);

        var logger = Services.GetRequiredService<ILogger<App>>();

        // The POS must never start on a stale schema: verify the database is reachable before the
        // first cashier touches the keyboard.
        try
        {
            var store = await Services.GetRequiredService<IStoreSettingsService>().GetAsync();
            logger.LogInformation("POS started for store {StoreName}", store.StoreName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The POS could not reach the database");
            ArabicMessageBox.ShowError(
                "تعذر الاتصال بقاعدة بيانات النظام.\n" +
                "برجاء التأكد من تشغيل SQL Server ومراجعة إعدادات الاتصال.\n\n" + ex.Message,
                "خطأ في الاتصال");

            Shutdown(-1);
            return;
        }

        var loginWindow = Services.GetRequiredService<LoginWindow>();
        MainWindow = loginWindow;
        loginWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(Configuration);
        services.AddSupermarketCore(Configuration, "POS");

        services.AddSingleton<IDialogService, DialogService>();

        // The thermal printer is a singleton: it caches the resolved printer name.
        services.AddSingleton<IReceiptPrinterService, ThermalPrinterService>();

        // Windows are resolved from the container so their dependencies (view model + services) are
        // validated on build instead of failing the first time a cashier opens the screen.
        // LoginWindow is the only window the container creates; SalesWindow takes the signed-in
        // cashier as a constructor argument and is created by the sign-in handler.
        services.AddTransient<LoginWindow>();
        services.AddSingleton<LoginViewModel>();
        services.AddTransient<SalesViewModel>();
        services.AddTransient<PaymentViewModel>();
        services.AddTransient<ShiftCloseViewModel>();
    }
}
