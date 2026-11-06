using System.ComponentModel;

namespace Supermarket.Core.Common;

/// <summary>
/// Small helper used by the view models to translate enum values into Arabic UI text
/// without leaking localized strings into the code base itself.
/// </summary>
public static class EnumArabicExtensions
{
    /// <summary>Arabic display text for <see cref="StockMovementType"/>.</summary>
    public static string ToArabic(this StockMovementType type) => type switch
    {
        StockMovementType.Purchase => "توريد",
        StockMovementType.Sale => "بيع",
        StockMovementType.SaleReturn => "مرتجع بيع",
        StockMovementType.PurchaseReturn => "مرتجع توريد",
        StockMovementType.AdjustmentIn => "تسوية بالزيادة",
        StockMovementType.AdjustmentOut => "تسوية بالنقص",
        StockMovementType.StockCount => "جرد",
        StockMovementType.Damage => "تالف",
        _ => "غير معروف"
    };

    /// <summary>Arabic display text for <see cref="PaymentMethod"/>.</summary>
    public static string ToArabic(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "نقدي",
        PaymentMethod.Card => "بطاقة",
        PaymentMethod.Wallet => "محفظة إلكترونية",
        PaymentMethod.Credit => "آجل",
        _ => "غير معروف"
    };

    /// <summary>Arabic display text for <see cref="ShiftStatus"/>.</summary>
    public static string ToArabic(this ShiftStatus status) => status switch
    {
        ShiftStatus.Open => "مفتوحة",
        ShiftStatus.Closed => "مقفلة",
        ShiftStatus.Suspended => "معلقة",
        _ => "غير معروف"
    };

    /// <summary>Arabic display text for <see cref="InvoiceStatus"/>.</summary>
    public static string ToArabic(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "مسودة",
        InvoiceStatus.Completed => "مكتملة",
        InvoiceStatus.Cancelled => "ملغاة",
        InvoiceStatus.Refunded => "مرتجعة",
        _ => "غير معروف"
    };

    /// <summary>Arabic display text for <see cref="PurchaseOrderStatus"/>.</summary>
    public static string ToArabic(this PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "مسودة",
        PurchaseOrderStatus.Ordered => "تم الطلب",
        PurchaseOrderStatus.PartiallyReceived => "مستلم جزئياً",
        PurchaseOrderStatus.Received => "تم الاستلام",
        PurchaseOrderStatus.Cancelled => "ملغاة",
        _ => "غير معروف"
    };

    /// <summary>Arabic display text for <see cref="UserRole"/>.</summary>
    public static string ToArabic(this UserRole role) => role switch
    {
        UserRole.Administrator => "مدير النظام",
        UserRole.Manager => "مشرف",
        UserRole.Cashier => "كاشير",
        UserRole.StockKeeper => "أمين مخزن",
        _ => "غير معروف"
    };
}
