namespace Supermarket.Core.Common;

/// <summary>Direction and reason of a stock movement.</summary>
public enum StockMovementType
{
    Purchase = 1,
    Sale = 2,
    SaleReturn = 3,
    PurchaseReturn = 4,
    AdjustmentIn = 5,
    AdjustmentOut = 6,
    StockCount = 7,
    Damage = 8
}

/// <summary>Payment instrument used to settle an invoice.</summary>
public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    Wallet = 3,
    Credit = 4
}

/// <summary>Lifecycle of an invoice.</summary>
public enum InvoiceStatus
{
    Draft = 0,
    Completed = 1,
    Cancelled = 2,
    Refunded = 3
}

/// <summary>Lifecycle of a purchase order.</summary>
public enum PurchaseOrderStatus
{
    Draft = 0,
    Ordered = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4
}

/// <summary>Cashier shift lifecycle. A cashier may not sell without an open shift.</summary>
public enum ShiftStatus
{
    Open = 1,
    Closed = 2,
    Suspended = 3
}

/// <summary>Role based access levels.</summary>
public enum UserRole
{
    Administrator = 1,
    Manager = 2,
    Cashier = 3,
    StockKeeper = 4
}

/// <summary>Kind of Z / X shift report.</summary>
public enum ShiftReportType
{
    /// <summary>X-report: read only snapshot, the shift keeps running.</summary>
    XReport = 1,

    /// <summary>Z-report: final closing snapshot produced when the shift is closed.</summary>
    ZReport = 2
}
