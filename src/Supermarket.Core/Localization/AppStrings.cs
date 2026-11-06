namespace Supermarket.Core.Localization;

/// <summary>
/// Single source of truth for the Arabic UI text shared by both WPF applications.
/// XAML binds to these values through the <c>x:Static</c> members exposed by the
/// <c>Loc</c> markup extension of each application, which keeps the C# code base English
/// while the user interface stays fully Arabic.
/// </summary>
public static class AppStrings
{
    // ------------------------------------------------------------- Application
    public const string AppTitle = "نظام إدارة السوبر ماركت";
    public const string AdminTitle = "نظام إدارة السوبر ماركت - لوحة التحكم";
    public const string PosTitle = "نظام إدارة السوبر ماركت - نقطة البيع";
    public const string Ok = "موافق";
    public const string Cancel = "إلغاء";
    public const string Save = "حفظ";
    public const string Delete = "حذف";
    public const string Edit = "تعديل";
    public const string Add = "إضافة";
    public const string Search = "بحث";
    public const string Refresh = "تحديث";
    public const string Close = "إغلاق";
    public const string Print = "طباعة";
    public const string Exit = "خروج";
    public const string Confirm = "تأكيد";
    public const string Yes = "نعم";
    public const string No = "لا";
    public const string Loading = "جارٍ التحميل...";
    public const string NoData = "لا توجد بيانات للعرض";
    public const string Error = "خطأ";
    public const string Warning = "تنبيه";
    public const string Success = "تم بنجاح";
    public const string Required = "هذا الحقل مطلوب";

    // ------------------------------------------------------------------ Login
    public const string Login = "تسجيل الدخول";
    public const string Logout = "تسجيل الخروج";
    public const string UserName = "اسم المستخدم";
    public const string Password = "كلمة المرور";
    public const string Pin = "الرقم السري";
    public const string LoginFailed = "تعذر تسجيل الدخول";

    // ---------------------------------------------------------------- Catalog
    public const string Products = "المنتجات";
    public const string Product = "المنتج";
    public const string ProductName = "اسم المنتج";
    public const string Barcode = "البار كود";
    public const string Category = "القسم";
    public const string Categories = "الأقسام";
    public const string Unit = "الوحدة";
    public const string Supplier = "المورد";
    public const string Suppliers = "الموردون";
    public const string Customer = "العميل";
    public const string CostPrice = "سعر التكلفة";
    public const string UnitPrice = "سعر الوحدة";
    public const string SellingPrice = "سعر البيع";
    public const string DiscountPrice = "سعر الخصم";
    public const string StockQuantity = "الكمية بالمخزن";
    public const string MinimumStockLevel = "حد الطلب الأدنى";
    public const string ReorderQuantity = "كمية إعادة الطلب";
    public const string ShelfLocation = "مكان التخزين";
    public const string IsWeighted = "يُباع بالوزن";
    public const string IsActive = "مُفعّل";
    public const string Description = "الوصف";
    public const string VatRate = "نسبة الضريبة";
    public const string Margin = "هامش الربح";
    public const string StockValue = "قيمة المخزون";
    public const string InitialStock = "رصيد افتتاحي";

    // -------------------------------------------------------------- Inventory
    public const string Inventory = "المخزون";
    public const string LowStock = "نواقص المخزون";
    public const string LowStockAlert = "تنبيه: منتجات وصلت إلى حد الطلب الأدنى";
    public const string OutOfStock = "منتجات غير متوفرة";
    public const string StockMovements = "حركة المخزون";
    public const string StockAdjustment = "تسوية مخزون";
    public const string StockCount = "جرد المخزون";

    // ------------------------------------------------------------------- Sales
    public const string Sales = "المبيعات";
    public const string NewSale = "بيع جديد";
    public const string Invoice = "الفاتورة";
    public const string InvoiceNumber = "رقم الفاتورة";
    public const string Quantity = "الكمية";
    public const string Subtotal = "المجموع الفرعي";
    public const string Total = "الإجمالي";
    public const string NetTotal = "الصافي";
    public const string Discount = "الخصم";
    public const string Tax = "الضريبة";
    public const string ChangeDue = "المتبقي";
    public const string Paid = "المدفوع";
    public const string Remaining = "الباقي";
    public const string Payment = "الدفع";
    public const string PaymentMethod = "طريقة الدفع";
    public const string Cash = "نقدي";
    public const string Card = "بطاقة";
    public const string Wallet = "محفظة إلكترونية";
    public const string Credit = "آجل";
    public const string Cashier = "الكاشير";
    public const string WalkInCustomer = "عميل نقدي";
    public const string EmptyCart = "السلة فارغة";
    public const string ItemAdded = "تمت إضافة المنتج إلى السلة";
    public const string ItemNotFound = "المنتج غير موجود";
    public const string InsufficientStock = "الكمية المتوفرة غير كافية";
    public const string SaleCompleted = "تم إتمام عملية البيع بنجاح";
    public const string HoldSale = "تعليق الفاتورة";
    public const string ResumeSale = "استرجاع الفاتورة";

    // --------------------------------------------------------------- Printing
    public const string Receipt = "إيصال البيع";
    public const string ReceiptFooter = "شكراً لزيارتكم";
    public const string PrintReceipt = "طباعة الإيصال";
    public const string ReprintReceipt = "إعادة طباعة الإيصال";
    public const string PrinterNotFound = "لم يتم العثور على طابعة الإيصالات";

    // ----------------------------------------------------------------- Shifts
    public const string Shift = "الورديّة";
    public const string ShiftClosing = "تقفيل الورديّة";
    public const string OpenShift = "فتح ورديّة";
    public const string CloseShift = "إغلاق الورديّة";
    public const string OpeningCash = "النقدية الافتتاحية";
    public const string ClosingCash = "النقدية عند التقفيل";
    public const string ExpectedCash = "النقدية المتوقعة";
    public const string CashDifference = "فرق النقدية";
    public const string ZReport = "تقرير الورديّة (Z)";
    public const string XReport = "تقرير الورديّة (X)";
    public const string CashierShortage = "عجز في النقدية";
    public const string CashierSurplus = "زيادة في النقدية";

    // ------------------------------------------------------------------ Admin
    public const string Dashboard = "لوحة التحكم";
    public const string Reports = "التقارير";
    public const string Analytics = "التحليلات";
    public const string Users = "المستخدمون";
    public const string Roles = "الصلاحيات والأدوار";
    public const string Settings = "الإعدادات";
    public const string TodaySales = "مبيعات اليوم";
    public const string MonthSales = "مبيعات الشهر";
    public const string InvoiceCount = "عدد الفواتير";
    public const string TopProducts = "الأكثر مبيعاً";
    public const string FromDate = "من تاريخ";
    public const string ToDate = "إلى تاريخ";
    public const string Apply = "تطبيق";
    public const string Export = "تصدير";
}
