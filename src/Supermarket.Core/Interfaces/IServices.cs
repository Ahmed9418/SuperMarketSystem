using Supermarket.Core.Common;
using Supermarket.Core.DTOs;

namespace Supermarket.Core.Interfaces;

/// <summary>Read / write contract for the product catalog.</summary>
public interface IProductService
{
    /// <summary>Paged, filtered and sorted product list for the admin grid.</summary>
    Task<PagedResult<ProductDto>> GetPagedAsync(ProductFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Single product projection, or null when it does not exist.</summary>
    Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exact barcode match used by the POS barcode listener. Returns
    /// <see cref="ProductLookupResultDto.NotFound"/> when nothing matches.
    /// </summary>
    Task<ProductLookupResultDto> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    /// <summary>Quick search by Arabic name fragment, used by the POS search box.</summary>
    Task<IReadOnlyList<ProductDto>> SearchAsync(string term, int take = 40, CancellationToken cancellationToken = default);

    /// <summary>Products at or below their alert threshold, worst first.</summary>
    Task<IReadOnlyList<ProductDto>> GetLowStockAsync(int take = 100, CancellationToken cancellationToken = default);

    /// <summary>Creates a new product and writes the initial stock movement plus price history.</summary>
    Task<int> CreateAsync(ProductCreateUpdateDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing product and records the price changes in the audit trail.</summary>
    Task UpdateAsync(ProductCreateUpdateDto dto, CancellationToken cancellationToken = default);

    /// <summary>Soft delete / restore.</summary>
    Task SetActiveAsync(int id, bool isActive, string? userName = null, CancellationToken cancellationToken = default);

    /// <summary>Creates the next unused internal barcode ("20" + 11 digits).</summary>
    Task<string> GenerateBarcodeAsync(CancellationToken cancellationToken = default);

    /// <summary>True when another product already owns this barcode.</summary>
    Task<bool> IsBarcodeTakenAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default);
}

/// <summary>Lookup data shared by every combo box of both applications.</summary>
public interface ICatalogService
{
    Task<IReadOnlyList<LookupDto>> GetCategoriesAsync(bool onlyActive = true, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetUnitsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetSuppliersAsync(bool onlyActive = true, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetCustomersAsync(CancellationToken cancellationToken = default);
}

/// <summary>Supplier master data operations.</summary>
public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetPagedAsync(string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<SupplierDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(SupplierDto dto, CancellationToken cancellationToken = default);

    Task UpdateAsync(SupplierDto dto, CancellationToken cancellationToken = default);

    Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a supplier (soft delete). Products keep working: their supplier link is cleared first,
    /// and the delete is refused while purchase orders still reference the supplier.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read models and manual corrections for the stock ledger (Phase 2 – inventory management).
/// </summary>
public interface IInventoryService
{
    /// <summary>Paged, filtered stock movement history (كارت الصنف / حركة المخزون).</summary>
    Task<PagedResult<StockMovementDto>> GetMovementsAsync(StockMovementFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Ledger of a single product, newest first.</summary>
    Task<IReadOnlyList<StockMovementDto>> GetProductMovementsAsync(int productId, int take = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a manual correction (adjustment in / out, damage, stock count difference).
    /// Requires a reason and refuses to drive the balance below zero.
    /// </summary>
    Task<decimal> AdjustAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Create / update / activate operations for the product categories.</summary>
public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive = true, CancellationToken cancellationToken = default);

    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(CategoryDto dto, CancellationToken cancellationToken = default);

    Task UpdateAsync(CategoryDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deactivates or re-activates a category; refused when it still holds active products.</summary>
    Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a category (soft delete) after moving its products to the fallback category.
    /// Refused while it is used as the parent of another category.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Create / update operations for the measurement units.</summary>
public interface IUnitService
{
    Task<IReadOnlyList<UnitDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UnitDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(UnitDto dto, CancellationToken cancellationToken = default);

    Task UpdateAsync(UnitDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes a unit (soft delete); refused while products still use it.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Single write entry point for stock changes.</summary>
public interface IStockService
{
    /// <summary>
    /// Applies a signed quantity change, keeps <see cref="Entities.Product.StockQuantity"/> in sync and
    /// appends a ledger row. Returns the resulting on-hand quantity.
    /// </summary>
    Task<decimal> AdjustStockAsync(
        int productId,
        decimal quantityDelta,
        StockMovementType movementType,
        string? referenceNumber = null,
        string? reason = null,
        string? performedBy = null,
        int? invoiceId = null,
        int? purchaseOrderId = null,
        int? shiftId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stock ledger of one product, newest first.</summary>
    Task<IReadOnlyList<Entities.StockMovement>> GetMovementsAsync(int productId, int take = 100, CancellationToken cancellationToken = default);
}

/// <summary>Generates the human readable document numbers.</summary>
public interface INumberSequenceService
{
    /// <summary>e.g. INV-20261105-0007</summary>
    Task<string> NextInvoiceNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>e.g. SH-20261105-0003</summary>
    Task<string> NextShiftNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>e.g. PO-20261105-0002</summary>
    Task<string> NextPurchaseOrderNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>e.g. SC-20261105-0001</summary>
    Task<string> NextStockCountNumberAsync(CancellationToken cancellationToken = default);
}

/// <summary>Aggregated counters for the admin dashboard.</summary>
public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Checkout engine used by the POS. Persists an invoice, its lines, its payments and the matching
/// stock movements inside a single database transaction.
/// </summary>
public interface ISalesService
{
    /// <summary>
    /// Prices a cart without touching the database, using the store VAT configuration.
    /// Used by the POS to show live totals while the cashier is still scanning.
    /// </summary>
    Task<CartPricingDto> PriceCartAsync(
        IReadOnlyList<CartLineDto> lines,
        decimal invoiceDiscount = 0m,
        decimal invoiceDiscountPercentage = 0m,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the sale: re-validates prices and stock against the database, writes the invoice,
    /// deducts the stock through the ledger and settles a credit customer balance when needed.
    /// </summary>
    Task<SaleResultDto> CompleteSaleAsync(SaleRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Loads a completed invoice for reprinting (Phase 4 receipt service).</summary>
    Task<SaleResultDto?> GetInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default);
}

/// <summary>Parking and resuming POS baskets (held carts).</summary>
public interface IHeldCartService
{
    /// <summary>Stores the basket and returns the id of the held row.</summary>
    Task<int> HoldAsync(HoldCartRequest request, CancellationToken cancellationToken = default);

    /// <summary>The cashier's parked baskets, newest first.</summary>
    Task<IReadOnlyList<HeldCartDto>> GetHeldAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-prices the basket from the database, deletes the held row and returns the lines ready to be
    /// pushed back into the cart. Returns null when the row no longer exists.
    /// </summary>
    Task<HeldCartDto?> ResumeAsync(int heldCartId, CancellationToken cancellationToken = default);

    /// <summary>Drops a parked basket without resuming it.</summary>
    Task<bool> DeleteAsync(int heldCartId, CancellationToken cancellationToken = default);

    /// <summary>Badge counter for the sales screen.</summary>
    Task<int> CountAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>Shift lifecycle needed by the POS before and during a selling session.</summary>
public interface IShiftService
{
    /// <summary>Open shift of a cashier on a terminal, or null when the cashier has not opened one.</summary>
    Task<ShiftDto?> GetOpenShiftAsync(int userId, string terminalId, CancellationToken cancellationToken = default);

    /// <summary>Returns the open shift, opening a new one when none exists.</summary>
    Task<ShiftDto> GetOrOpenAsync(ShiftOpenRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the figures of the shift straight from the invoices (X report: the shift keeps running,
    /// nothing is written to the database).
    /// </summary>
    Task<ShiftReportDto> BuildReportAsync(int shiftId, ShiftReportType reportType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the shift: recomputes the figures, stores the counted cash, the expected cash and the
    /// difference, persists the Z report (with the printed text) and marks the shift as closed.
    /// </summary>
    Task<ShiftReportDto> CloseShiftAsync(ShiftCloseRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Stored reports of a shift, newest first (used for reprinting a Z report).</summary>
    Task<IReadOnlyList<ShiftReportDto>> GetReportsAsync(int shiftId, CancellationToken cancellationToken = default);

    /// <summary>Shifts of a terminal within a date range, newest first (shift history screen).</summary>
    Task<IReadOnlyList<ShiftDto>> GetShiftsAsync(DateTime fromDate, DateTime toDate, string? terminalId = null, int take = 100, CancellationToken cancellationToken = default);
}

/// <summary>Reads and caches the single-row store settings.</summary>
public interface IStoreSettingsService
{
    Task<Entities.StoreSetting> GetAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.StoreSetting settings, CancellationToken cancellationToken = default);

    /// <summary>Drops the in-memory cache so the next read hits the database.</summary>
    void Invalidate();
}

/// <summary>Authentication and authorization shared by both applications.</summary>
public interface IAuthService
{
    Task<AuthResult> SignInAsync(string userName, string password, CancellationToken cancellationToken = default);

    /// <summary>Fast POS login with the 4 digit PIN.</summary>
    Task<AuthResult> SignInWithPinAsync(string userName, string pin, CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
