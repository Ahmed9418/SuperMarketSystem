using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supermarket.Core.DependencyInjection;
using Supermarket.Core.Interfaces;
using Supermarket.POS.Printing;
using Supermarket.POS.Services;
using Supermarket.POS.ViewModels;
using Supermarket.POS.Views;

namespace Supermarket.POS;

/// <summary>
/// Composition root of the point of sale terminal. Phase 1 ships the start-up pipeline
/// (culture, RTL metadata, DI, configuration, database readiness check) so Phase 3 can focus
/// exclusively on the sales screen.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static IConfiguration Configuration { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var culture = new CultureInfo("ar-EG");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

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

        base.OnStartup(e);

        var logger = Services.GetRequiredService<ILogger<App>>();

        // The POS must never start on a stale schema: verify the migrations are applied
        // before the first cashier touches the keyboard.
        try
        {
            var settingsService = Services.GetRequiredService<IStoreSettingsService>();
            var store = await settingsService.GetAsync();
            logger.LogInformation("POS started for store {StoreName}", store.StoreName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The POS could not reach the database");
            MessageBox.Show(
                "تعذر الاتصال بقاعدة بيانات النظام.\nبرجاء التأكد من تشغيل SQL Server ومراجعة إعدادات الاتصال.\n\n" + ex.Message,
                "خطأ في الاتصال",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            Shutdown(-1);
            return;
        }

        var loginWindow = ActivatorUtilities.CreateInstance<LoginWindow>(Services);
        MainWindow = loginWindow;
        loginWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(Configuration);
        services.AddSupermarketCore(Configuration, "POS");

        services.AddSingleton<IDialogService, DialogService>();

        // Windows presentation: the thermal printer is a singleton (it holds the resolved
        // printer name) and lives in the POS application because it needs the spooler.
        services.AddSingleton<IReceiptPrinterService, ThermalPrinterService>();

        services.AddSingleton<LoginViewModel>();
        services.AddTransient<SalesViewModel>();
        services.AddTransient<PaymentViewModel>();
        services.AddTransient<ShiftCloseViewModel>();
    }

}
