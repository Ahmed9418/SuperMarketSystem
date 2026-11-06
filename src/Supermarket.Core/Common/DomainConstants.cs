namespace Supermarket.Core.Common;

/// <summary>
/// Central place for the numeric / string constants that both applications must agree on.
/// Keeping them here prevents the "magic number" drift that usually appears between
/// an admin application and a point of sale terminal.
/// </summary>
public static class DomainConstants
{
    /// <summary>Egyptian Pound symbol appended to every financial label in the UI.</summary>
    public const string CurrencySymbol = "ج.م";

    /// <summary>Barcode value used when the cashier sells a loose (non barcoded) item.</summary>
    public const string LooseItemBarcodePrefix = "2000000";

    /// <summary>Maximum number of characters allowed in a product name.</summary>
    public const int ProductNameMaxLength = 200;

    /// <summary>Maximum number of characters allowed in a barcode.</summary>
    public const int BarcodeMaxLength = 50;

    /// <summary>Number of decimals used for money columns (EGP has 2 minor units).</summary>
    public const int MoneyPrecision = 18;

    /// <summary>Number of decimals kept for quantities (supports weighted goods).</summary>
    public const int QuantityPrecision = 18;

    public const int MoneyScale = 2;

    public const int QuantityScale = 3;

    public const int UnitPriceScale = 4;

    /// <summary>Default VAT applied to taxable lines when no product specific value exists.</summary>
    public const decimal DefaultVatRate = 0.14m;

    /// <summary>Name of the seeded administrator account.</summary>
    public const string DefaultAdminUserName = "admin";

    /// <summary>Name of the seeded cashier account.</summary>
    public const string DefaultCashierUserName = "cashier";
}
