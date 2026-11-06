using Supermarket.Core.Entities;

namespace Supermarket.Core.Common;

/// <summary>
/// The permission catalog of the application, plus the default grants of every role.
///
/// <para>
/// A permission is a plain string key persisted in <see cref="UserPermission"/> only when it *differs*
/// from the role default: a missing row means "use the role default". That keeps the table tiny and
/// makes a future change of a role default apply to every user who never overrode it.
/// </para>
/// </summary>
public static class AppPermissions
{
    // ------------------------------------------------------------------ Screens & actions
    public const string DashboardView = "Dashboard.View";
    public const string ProductsView = "Products.View";
    public const string ProductsEdit = "Products.Edit";
    public const string StockView = "Stock.View";
    public const string StockAdjust = "Stock.Adjust";
    public const string SuppliersView = "Suppliers.View";
    public const string SuppliersEdit = "Suppliers.Edit";
    public const string CatalogView = "Catalog.View";
    public const string CatalogEdit = "Catalog.Edit";
    public const string InvoicesView = "Invoices.View";
    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";
    public const string UsersView = "Users.View";
    public const string UsersManage = "Users.Manage";
    public const string SettingsView = "Settings.View";
    public const string SettingsEdit = "Settings.Edit";
    public const string PosSell = "Pos.Sell";
    public const string PosDiscount = "Pos.Discount";
    public const string PosShiftClose = "Pos.ShiftClose";

    /// <summary>Every key with its Arabic caption, in the order shown on the permissions screen.</summary>
    public static readonly IReadOnlyList<(string Key, string ArabicName, string Group)> Catalog = new[]
    {
        (DashboardView, "عرض لوحة التحكم", "عام"),
        (ProductsView, "عرض المنتجات", "المنتجات"),
        (ProductsEdit, "إضافة وتعديل المنتجات", "المنتجات"),
        (CatalogView, "عرض الأقسام ووحدات القياس", "المنتجات"),
        (CatalogEdit, "تعديل الأقسام ووحدات القياس", "المنتجات"),
        (StockView, "عرض حركة المخزون", "المخزون"),
        (StockAdjust, "تسوية المخزون", "المخزون"),
        (SuppliersView, "عرض الموردين", "الموردون"),
        (SuppliersEdit, "إضافة وتعديل الموردين", "الموردون"),
        (InvoicesView, "عرض الفواتير", "المبيعات"),
        (ReportsView, "عرض التقارير والتحليلات", "المبيعات"),
        (ReportsExport, "تصدير التقارير إلى ملف", "المبيعات"),
        (PosSell, "البيع على نقطة البيع", "نقطة البيع"),
        (PosDiscount, "منح خصم على الفاتورة", "نقطة البيع"),
        (PosShiftClose, "تقفيل الورديّة", "نقطة البيع"),
        (UsersView, "عرض المستخدمين", "الإدارة"),
        (UsersManage, "إدارة المستخدمين والصلاحيات", "الإدارة"),
        (SettingsView, "عرض الإعدادات", "الإدارة"),
        (SettingsEdit, "تعديل الإعدادات", "الإدارة")
    };

    /// <summary>Default grants of each role, used when a user has no override row.</summary>
    public static IReadOnlySet<string> DefaultsFor(UserRole role) => role switch
    {
        UserRole.Administrator => Catalog.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),

        UserRole.Manager => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DashboardView, ProductsView, ProductsEdit, CatalogView, CatalogEdit, StockView, StockAdjust,
            SuppliersView, SuppliersEdit, InvoicesView, ReportsView, ReportsExport, SettingsView,
            PosSell, PosDiscount, PosShiftClose, UsersView
        },

        UserRole.Cashier => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DashboardView, ProductsView, InvoicesView, PosSell, PosShiftClose
        },

        UserRole.StockKeeper => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DashboardView, ProductsView, ProductsEdit, CatalogView, StockView, StockAdjust,
            SuppliersView, SuppliersEdit
        },

        _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    };

    /// <summary>Arabic caption of a key (falls back to the raw key when unknown).</summary>
    public static string ArabicNameOf(string key) =>
        Catalog.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).ArabicName ?? key;
}
