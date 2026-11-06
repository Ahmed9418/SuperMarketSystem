using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Localization;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Shell of the admin application: owns the navigation and the menu commands.
/// Every page is resolved from the DI container on demand, which keeps start-up fast.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly AuthSession _session;
    private readonly IDialogService _dialogs;
    private readonly IStoreSettingsService _settings;

    public MainViewModel(
        IServiceProvider services,
        AuthSession session,
        IDialogService dialogs,
        NavigationService navigation,
        IStoreSettingsService settings)
    {
        _services = services;
        _session = session;
        _dialogs = dialogs;
        _settings = settings;
        Navigation = navigation;

        Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationService.CurrentViewModel))
            {
                OnPropertyChanged(nameof(CurrentPageTitle));
            }
        };
    }

    public NavigationService Navigation { get; }

    public string DisplayName => _session.DisplayName;

    public string RoleName => _session.RoleNameArabic;

    /// <summary>Store name from the settings table (falls back to the seeded default until loaded).</summary>
    [ObservableProperty]
    private string storeName = "سوبر ماركت الأمانة";

    /// <summary>Today's date, shown in the header. Latin digits keep it readable next to Arabic labels.</summary>
    public string TodayText => DateTime.Now.ToString("dd/MM/yyyy", AppCulture.Culture);

    [ObservableProperty]
    private bool isBusy;

    /// <summary>Arabic title shown in the shell header for the active page.</summary>
    public string CurrentPageTitle => Navigation.CurrentViewModel switch
    {
        DashboardViewModel => "لوحة التحكم",
        ProductsViewModel => "إدارة المنتجات",
        SuppliersViewModel => "إدارة الموردين",
        SalesAnalyticsViewModel => "المبيعات والتحليلات",
        UsersViewModel => "المستخدمون والصلاحيات",
        SettingsViewModel => "إعدادات المتجر",
        LowStockViewModel => "نواقص المخزون",
        StockMovementsViewModel => "حركة المخزون",
        CatalogViewModel => "الأقسام ووحدات القياس",
        _ => "لوحة التحكم"
    };

    /// <summary>Loads the store header and opens the dashboard as the first page.</summary>
    public async Task InitializeAsync()
    {
        await LoadStoreNameAsync();
        NavigateToDashboard();
    }

    private async Task LoadStoreNameAsync()
    {
        try
        {
            var settings = await _settings.GetAsync();
            if (!string.IsNullOrWhiteSpace(settings.StoreName))
            {
                StoreName = settings.StoreName;
            }
        }
        catch
        {
            // A missing settings row must never block the shell: the seeded name stays.
        }
    }

    [RelayCommand]
    private void NavigateToDashboard() => Show<DashboardViewModel>();

    [RelayCommand]
    private void NavigateToProducts() => Show<ProductsViewModel>();

    [RelayCommand]
    private void NavigateToSuppliers() => Show<SuppliersViewModel>();

    [RelayCommand]
    private void NavigateToStockMovements() => Show<StockMovementsViewModel>();

    [RelayCommand]
    private void NavigateToCatalog() => Show<CatalogViewModel>();

    [RelayCommand]
    private void NavigateToLowStock() => Show<LowStockViewModel>();

    [RelayCommand]
    private void NavigateToAnalytics() => Show<SalesAnalyticsViewModel>();

    [RelayCommand]
    private void NavigateToUsers() => Show<UsersViewModel>();

    [RelayCommand]
    private void NavigateToSettings() => Show<SettingsViewModel>();

    private void Show<TViewModel>() where TViewModel : ObservableObject =>
        Navigation.NavigateTo(_services.GetRequiredService<TViewModel>());

    // ------------------------------------------------------------------ Permissions
    public bool CanViewReports => _session.Can(AppPermissions.ReportsView) || _session.Can(AppPermissions.DashboardView);

    public bool CanViewUsers => _session.Can(AppPermissions.UsersView);

    public bool CanViewSettings => _session.Can(AppPermissions.SettingsView);

    public bool CanViewProducts => _session.Can(AppPermissions.ProductsView);

    public bool CanViewStock => _session.Can(AppPermissions.StockView);

    public bool CanViewCatalog => _session.Can(AppPermissions.CatalogView);

    public bool CanViewSuppliers => _session.Can(AppPermissions.SuppliersView);

    [RelayCommand]
    private void SignOut()
    {
        if (!_dialogs.Confirm("هل تريد تسجيل الخروج من النظام؟", "تسجيل الخروج"))
        {
            return;
        }

        _session.SignOut();
        System.Windows.Application.Current.Shutdown();
    }
}
