# Architecture — Supermarket Management System (Phase 1)

## 1. Layering

```
┌──────────────────────────┐        ┌──────────────────────────┐
│   Supermarket.Admin      │        │    Supermarket.POS       │
│   (WPF, net8.0-windows)  │        │   (WPF, net8.0-windows)  │
│  Views (XAML, RTL)       │        │  Views (XAML, RTL)       │
│  ViewModels (Toolkit)    │        │  ViewModels (Toolkit)    │
│  UI services (dialogs,   │        │  Barcode listener,       │
│  session, navigation)    │        │  thermal printing (Ph.4) │
└───────────┬──────────────┘        └───────────┬──────────────┘
            │      ProjectReference             │
            └───────────────┬───────────────────┘
                            ▼
            ┌───────────────────────────────────┐
            │        Supermarket.Core           │
            │  Entities / DTOs / Interfaces      │
            │  SupermarketDbContext + Fluent API │
            │  Services (product, stock, auth…)  │
            │  Seed data + migrations            │
            └───────────────┬───────────────────┘
                            ▼
                    SQL Server (SupermarketDb)
```

The Core library has **no WPF reference**, therefore it can be unit tested and, if the project ever
needs a web API or a reporting service, the exact same model and services can be reused.

## 2. Data model

| Aggregate | Tables | Notes |
|---|---|---|
| Catalog | `Products`, `Categories`, `Units`, `Suppliers`, `Customers` | `Products.Barcode` unique among non-deleted rows; `Categories` self-referencing tree. |
| Inventory | `StockMovements`, `PriceHistories`, `StockCounts`, `StockCountItems` | `StockMovements` is an append-only ledger (signed quantity + balance after). |
| Security / store | `Users`, `UserPermissions`, `StoreSettings` | `UserPermissions` holds **only** the deviations from the role default (`AppPermissions.DefaultsFor`), so a role change propagates to everyone who never overrode it. |
| Sales | `Invoices`, `InvoiceItems`, `Payments`, `Shifts`, `ShiftReports` | One invoice belongs to one shift; the line snapshots the Arabic product name and the cost at sale time; every Z closing writes a `ShiftReports` row with the printed text. |
| POS operations | `HeldCarts` | Parked baskets as versioned JSON (`nvarchar(4000)`); re-priced from the catalog on resume, deleted once resumed. |
| Purchasing | `PurchaseOrders`, `PurchaseOrderItems` | Receiving writes inbound ledger rows. |

### Money & quantity precision

| Kind | Type |
|---|---|
| Invoice totals, payments, shift cash | `decimal(18,2)` |
| Product cost / selling / discount price | `decimal(18,4)` |
| Product quantity, movement quantity | `decimal(18,3)` |
| Percentages (VAT, discount %) | `decimal(9,4)` / `decimal(9,2)` |

### Stock integrity

`Product.StockQuantity` is a **cache**; `StockMovements` is the truth. The only component allowed to
change a quantity is `IStockService.AdjustStockAsync`, which
1. loads the product,
2. refuses to go below zero (Arabic-friendly exception message),
3. updates `StockQuantity`,
4. appends a `StockMovement` with `BalanceAfter`, `UnitCost`, the document reference and the operator.

Because both writes happen in the same `SaveChangesAsync` call, the ledger always reconciles with the
product row. The seeded opening balances are written as `Purchase` movements (`OPENING-2026`), and a
unit test (`Seed_StockLedgerReconcilesWithProductQuantities`) asserts that
`SUM(StockMovements.Quantity) == Product.StockQuantity` for every seeded product.

Manual corrections (`IInventoryService.AdjustAsync`) add a business layer on top of the ledger:

* quantity must be > 0 and a **reason is mandatory** (written to `StockMovements.Reason`);
* the chosen `StockMovementType` must agree with the chosen direction (an inbound type cannot be
  used to remove stock and vice-versa);
* decreasing below zero is refused by `IStockService`;
* the operator name and an `ADJ` reference are stored for audit.

### Audit & soft delete

`BaseEntity` gives every table `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`, `IsDeleted`.
`SupermarketDbContext.SaveChanges`/`SaveChangesAsync` overrides stamp these values from
`ICurrentUserService` and convert `EntityState.Deleted` into a soft delete. Every entity configuration
registers the global query filter `!IsDeleted`, so deleted master data disappears from the grids while
historical invoices keep resolving their references.

## 3. Dependency injection & lifetimes

`AddSupermarketCore(IConfiguration, "Admin" | "POS")` registers:

| Service | Lifetime | Why |
|---|---|---|
| `IDbContextFactory<SupermarketDbContext>` | singleton | creates a fresh context per call (deliberately **not** pooled: the context also takes the audit stamper). Registered with `ValidateScopes`/`ValidateOnBuild`, so a missing registration fails at start-up instead of when a window opens. |
| `SupermarketDbContext` | **transient** | a desktop app is long-lived — two view models must never share a context |
| `ICurrentUserService` / `MutableCurrentUserService` | singleton | audit stamping across the whole session; injected into the context through its single public constructor |
| `ILogger<T>` (via `AddLogging`) | singleton provider | the shared library is self-sufficient — tests, tooling and both shells construct the same graph |
| `IStoreSettingsService` | singleton | single-row settings cached for the session |
| `IDateTimeProvider` | singleton | testable clock |
| `IPasswordHasher<User>` | singleton | PBKDF2 hashing |
| `IReportService`, `IUserAdminService`, `INumberSequenceService`, `IStockService`, `IProductService`, `ICatalogService`, `ISupplierService`, `IInventoryService`, `ICategoryService`, `IUnitService`, `IDashboardService`, `IAuthService`, `ISalesService`, `IShiftService`, `IHeldCartService` | transient | each operation gets a clean context |
| `IReceiptFormatter` | singleton | stateless text layout (pure functions, asserted by unit tests) |

## 4. MVVM conventions

* `[ObservableProperty]` generates the public property from the private field (`userName` → `UserName`).
* `[RelayCommand]` on a `Task`-returning method generates `XxxCommand` of type `IAsyncRelayCommand`.
* Views never touch the database: they bind to DTOs, call commands, and use converters for display.
* Dialogs resolve their view model through `ActivatorUtilities.CreateInstance` so the scoped/transient
  Core services are injected without a service-locator in the view model.
* `IDialogService` wraps `MessageBox` so view models stay unit-testable.

## 5. Localization strategy

| Layer | Rule |
|---|---|
| XAML | Arabic literals with `FlowDirection="RightToLeft"`; windows inherit it from the implicit `Window` style. |
| Code | English identifiers, English XML comments. Arabic only appears in (a) user-facing status/validation messages produced by the services, (b) `Localization/AppStrings.cs`, (c) `EnumArabicExtensions`. |
| Culture | `ar-EG` set on start-up for both threads **and** `FrameworkElement.Language`, so dates and numbers render correctly. |
| Currency | `EgpCurrencyConverter` appends `ج.م` to every financial binding. |

## 6. Why the design-time migrations project exists

The two WPF projects target `net8.0-windows`, which the EF Core CLI cannot load on a Linux agent.
`tools/Supermarket.Migrations` is a small `net8.0` console project whose only purpose is to provide a
`IDesignTimeDbContextFactory<SupermarketDbContext>`, so:

```bash
dotnet ef migrations add <Name> \
  --project src/Supermarket.Core \
  --startup-project tools/Supermarket.Migrations \
  --output-dir Data/Migrations
```

works identically on Windows, Linux and CI.

## 7. Extensibility hooks prepared for later phases

| Phase | Hook already in place |
|---|---|
| 3 – POS screen | `IProductService.FindByBarcodeAsync`, `CartLineDto`, `SaleRequestDto`, `SaleResultDto` |
| 4 – Printing & shift closing | ✅ delivered — `IReceiptFormatter`, `IShiftService.BuildReportAsync/CloseShiftAsync`, `HeldCarts`, `Pos:PrinterName` |
| 5 – Analytics & roles | ✅ delivered — `ReportService` (`SalesAnalyticsDto`), `UserAdminService`, `AppPermissions` (19 keys + role defaults), permission-driven shells |

### Permission model (Phase 5)

```
AppPermissions.Catalog (key + Arabic caption + group)
        └── DefaultsFor(role)      → the grant set of the role
UserPermissions rows (IsGranted)   → explicit overrides, written only when they differ
        └── UserAdminService.GetEffectivePermissionsAsync(userId)
                  ├── AuthSession.Can(key)   → Admin shell visibility + POS gates
                  └── IUserAdminService guards the last administrator / self lock-out
```

Reporting is deliberately read-only and projection-only: `ReportService` never returns an entity, and
every figure is aggregated in SQL (daily series, payment split, per-cashier and per-category totals).

### Printing architecture (Phase 4)

WPF is used as the typesetting engine because ESC/POS text mode cannot join Arabic letters. The
formatter produces plain text, `EscPosImageBuilder` renders it at 576 dots (80 mm @ 203 dpi) and
converts the bitmap to 1 bpp, then `ThermalPrinterService` sends `GS v 0` raster bands through
`winspool.drv`. Printing is deliberately **outside** the checkout transaction: the invoice is always
committed first, and a missing printer only degrades to an on-screen preview.
