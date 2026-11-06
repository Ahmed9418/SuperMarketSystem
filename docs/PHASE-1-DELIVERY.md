# Phase 1 delivery — Core architecture & database foundation

Everything below was compiled with the .NET 8 SDK (`dotnet build SupermarketSystem.sln` → **0 warnings,
0 errors**) and the Core services were exercised by **20 passing xUnit tests**.

## 1. Deliverables

### Supermarket.Core (shared library, `net8.0`)

| Area | Files |
|---|---|
| Primitives | `Common/BaseEntity.cs`, `Common/DomainConstants.cs`, `Common/PagedResult.cs`, `Common/EnumArabicExtensions.cs`, `Entities/Enums.cs` |
| Entities | `Product`, `Category`, `Unit`, `Supplier`, `Customer`, `StockMovement`, `PriceHistory`, `StockCount`, `StockCountItem`, `Invoice`, `InvoiceItem`, `Payment`, `Shift`, `ShiftReport`, `PurchaseOrder`, `PurchaseOrderItem`, `User`, `UserPermission`, `StoreSetting` (19 tables) |
| Persistence | `Data/SupermarketDbContext.cs` (audit stamping, soft delete, convention precision), `Data/Configurations/*` (Fluent API + Unicode + filtered indexes), `Data/Seed/SeedData.cs`, `Data/Migrations/*` (InitialCreate) |
| Contracts | `Interfaces/IServices.cs`, `Interfaces/ICurrentUserService.cs` |
| DTOs | `DTOs/CatalogDtos.cs`, `DTOs/SalesDtos.cs`, `DTOs/ShiftDtos.cs` |
| Services | `ProductService`, `StockService`, `CatalogService`, `SupplierService`, `AuthService`, `DashboardService`, `NumberSequenceService`, `StoreSettingsService` |
| Composition | `DependencyInjection/CoreServiceCollectionExtensions.cs` (`AddSupermarketCore`) |
| Localization | `Localization/AppCulture.cs` (ar-EG culture with Latin digits) + `Supermarket.Wpf/ArabicLayout.cs` |

### Supermarket.Admin (WPF, `net8.0-windows`)

* RTL theme: `Themes/Colors.xaml`, `Themes/Controls.xaml` (window, text, buttons, DataGrid, cards).
* Converters: EGP currency (`ج.م`), stock status colours, enum → Arabic, visibility helpers.
* Shell: `LoginWindow` (password auth) → `MainWindow` (right-hand sidebar navigation, header, user block).
* Pages: `DashboardView` (KPIs + low-stock grid), `ProductsView` (search, filters, sorting, paging,
  colour-coded rows, inline stock-adjustment shortcut), `LowStockView` (reorder suggestions +
  estimated supply cost), `StockMovementsView` (stock card: date/type/date-range filters, inbound vs
  outbound value totals, paging), `CatalogView` (categories + units, two tabs with inline forms),
  `SuppliersView`.
* Dialogs: `ProductEditWindow` (full product form with live margin preview, auto barcode generation),
  `StockAdjustmentWindow` (تسوية مخزون: direction + movement type + mandatory reason + live
  balance-after preview), `SupplierEditWindow`, `InputDialog`.
* Infrastructure: `AuthSession`, `DialogService`, `NavigationService`, global exception handling,
  `appsettings.json`.

> Phase 2 is **complete** in this drop: product management, stock visibility *and* stock editing
> (adjustments + full movement history) plus catalog master data (categories, units). The admin
> application is therefore end-to-end (grid → dialog → service → SQL Server) and Phase 3 can start
> on the POS immediately.

### Supermarket.POS (WPF, `net8.0-windows`)

* Large touch-oriented RTL theme (`Themes/PosTheme.xaml`).
* `LoginViewModel` + `LoginWindow` with the 4-digit PIN fast login and a password fallback.
* Start-up pipeline: culture, RTL metadata, DI, store-settings read that fails loudly if the database
  is unreachable.
* Ready for Phase 3: the sales screen, barcode listener and F-key shortcuts plug into this shell.

### Tests & tooling

* `tests/Supermarket.Core.Tests` — **48 tests** (34 for phases 1–2, 14 for phase 3) covering model creation (all 19 tables), seed integrity
  (including the ledger ↔ quantity reconciliation invariant), Unicode Arabic storage, hashed
  credentials, precision configuration, soft delete, stock ledger consistency, manual stock
  adjustments (increase / damage / insufficient stock / mandatory reason / direction-vs-type guard),
  movement history paging & filters, categories (duplicate guard, cannot deactivate with active
  products, product counts), units (fractions flag, product counts), barcode lookup, low-stock
  filtering, product creation with opening stock, duplicate barcode rejection, price history,
  document numbering, dashboard aggregates, auth and suppliers.
* `tools/Supermarket.Migrations` — design-time host so `dotnet ef` works on any OS.
* `database/SupermarketDb_Schema.sql` — idempotent SQL Server script (schema + seed).
* `docs/ARCHITECTURE.md`, `docs/DATABASE-SETUP.md`, `README.md`.

## 2. Verification performed

```
$ dotnet build SupermarketSystem.sln
Build succeeded.  0 Warning(s)  0 Error(s)

$ dotnet test tests/Supermarket.Core.Tests
Passed! - Failed: 0, Passed: 34, Skipped: 0, Total: 34

$ dotnet ef migrations has-pending-model-changes --project src/Supermarket.Core --startup-project tools/Supermarket.Migrations
No changes have been made to the model since the last migration.
```

The XAML of both WPF applications is compiled by the WPF MSBuild tasks (`MarkupCompilePass`), so any
binding-path typo in a `DataTemplate` or a missing resource key would fail the build.

## 3. What Phase 3 will consume (already implemented)

| Need | Ready-to-use member |
|---|---|
| Barcode scan | `IProductService.FindByBarcodeAsync(barcode)` → `ProductLookupResultDto` (Arabic message included) |
| Quick search / touch tiles | `IProductService.SearchAsync(term, take)` |
| Cart line math | `CartLineDto` (`LineTotal`, `TaxAmount`, `LineTotalWithTax`) |
| Checkout payload | `SaleRequestDto` → `SaleResultDto` |
| Stock deduction | `IStockService.AdjustStockAsync(...)` with `StockMovementType.Sale`, `shiftId`, `invoiceId` |
| Invoice numbering | `INumberSequenceService.NextInvoiceNumberAsync()` → `INV-20261105-0001` |
| Shift handling | `Shift`, `ShiftOpenRequestDto`, `ShiftCloseRequestDto`, `ShiftReportDto` |
| Receipt header | `IStoreSettingsService.GetAsync()` (store name, address, tax number, line width, printer name) |

## 4. Suggested next step (Phase 3)

1. `SalesViewModel` + `SalesView` (RTL): barcode text box with auto-focus, cart `DataGrid`, totals panel.
2. `BarcodeListener` (keyboard wedge) that always routes keystrokes to the scan box.
3. F-key shortcut map: `F1` help, `F2` search, `F3` quantity, `F4` discount, `F8` hold, `F9` payment,
   `F10` shift close, `Esc` cancel line.
4. `SalesService` that persists `SaleRequestDto` in one transaction (invoice + items + payments + stock
   movements) and returns `SaleResultDto` for the receipt formatter.
