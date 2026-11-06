using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Shell of the admin application: owns the navigation service and the menu commands.
/// Every page is resolved from the DI container on demand, which keeps start-up time short.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly AuthSession _session;
    private readonly IDialogService _dialogs;

    public MainViewModel(IServiceProvider services, AuthSession session, IDialogService dialogs, NavigationService navigation)
    {
        _services = services;
        _session = session;
        _dialogs = dialogs;
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

    public string StoreName => "سوبر ماركت الأمانة";

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

    /// <summary>Loads the dashboard as the first page.</summary>
    public void Initialize() => NavigateToDashboard();

    [RelayCommand]
    private void NavigateToDashboard() => Navigation.NavigateTo(_services.GetRequiredService<DashboardViewModel>());

    [RelayCommand]
    private void NavigateToProducts() => Navigation.NavigateTo(_services.GetRequiredService<ProductsViewModel>());

    [RelayCommand]
    private void NavigateToSuppliers() => Navigation.NavigateTo(_services.GetRequiredService<SuppliersViewModel>());

    [RelayCommand]
    private void NavigateToStockMovements() => Navigation.NavigateTo(_services.GetRequiredService<StockMovementsViewModel>());

    [RelayCommand]
    private void NavigateToCatalog() => Navigation.NavigateTo(_services.GetRequiredService<CatalogViewModel>());

    [RelayCommand]
    private void NavigateToLowStock() => Navigation.NavigateTo(_services.GetRequiredService<LowStockViewModel>());

    [RelayCommand]
    private void NavigateToAnalytics() => Navigation.NavigateTo(_services.GetRequiredService<SalesAnalyticsViewModel>());

    [RelayCommand]
    private void NavigateToUsers() => Navigation.NavigateTo(_services.GetRequiredService<UsersViewModel>());

    [RelayCommand]
    private void NavigateToSettings() => Navigation.NavigateTo(_services.GetRequiredService<SettingsViewModel>());

    // ---------------------------------------------------------------- Permissions
    public bool CanViewReports => _session.Can(AppPermissions.ReportsView) || _session.Can(AppPermissions.DashboardView);

    public bool CanViewUsers => _session.Can(AppPermissions.UsersView);

    public bool CanViewSettings => _session.Can(AppPermissions.SettingsView);

    public bool CanViewProducts => _session.Can(AppPermissions.ProductsView);

    public bool CanViewStock => _session.Can(AppPermissions.StockView);

    public bool CanViewCatalog => _session.Can(AppPermissions.CatalogView);

    public bool CanViewSuppliers => _session.Can(AppPermissions.SuppliersView);

    [RelayCommand]
    private void ShowComingSoon(string? moduleName) =>
        _dialogs.ShowInformation($"شاشة ({moduleName}) ستكون متاحة في المرحلة القادمة من المشروع.", "قريباً");

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
