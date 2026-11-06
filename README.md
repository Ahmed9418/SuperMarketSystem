# نظام إدارة السوبر ماركت — Supermarket Management System

Dual-application supermarket management system built with **C# / WPF on .NET 8**, sharing a single
**SQL Server** database through **Entity Framework Core 8**.

* **Supermarket.Admin** — inventory, suppliers, products, analytics, roles (Arabic RTL UI).
* **Supermarket.POS** — high-speed cashier checkout, barcode listener, 80 mm thermal receipts, shift closing.
* **Supermarket.Core** — shared class library: entities, `DbContext`, Fluent API configuration, seed data,
  DTOs, interfaces and business services used by both executables.

> All UI text is Arabic with `FlowDirection="RightToLeft"`, all C# code, entities, properties,
> methods and comments are in English, and every financial label carries the **ج.م** suffix.

---

## 1. Solution layout

```
SupermarketSystem/
├── SupermarketSystem.sln
├── Directory.Build.props              # shared compiler settings (nullable, C# 12, ar-EG)
├── src/
│   ├── Supermarket.Core/              # ── shared library (net8.0)
│   │   ├── Common/                    # BaseEntity, enums, constants, paging, Arabic enum text
│   │   ├── Entities/                  # Product, Category, Unit, Supplier, Customer,
│   │   │                              # StockMovement, PriceHistory, StockCount(+Item),
│   │   │                              # Invoice(+Item), Payment, Shift, ShiftReport,
│   │   │                              # PurchaseOrder(+Item), HeldCart, User, UserPermission,
│   │   │                              # StoreSetting
│   │   ├── Data/
│   │   │   ├── SupermarketDbContext.cs        # DbSets, audit stamping, soft delete
│   │   │   ├── Configurations/                # Fluent API, one file per aggregate
│   │   │   ├── Seed/SeedData.cs               # deterministic HasData seed
│   │   │   └── Migrations/                    # EF Core migrations (InitialCreate)
│   │   ├── DTOs/                      # product / inventory / catalog / sale / shift contracts
│   │                              # (CartLineDto, CartPricingDto, SaleRequestDto, SaleResultDto …)
│   │   ├── Interfaces/                # service contracts + ICurrentUserService, IDateTimeProvider
│   │   ├── Services/                  # Product, Stock, Inventory, Category, Unit, Supplier,
│   │   │                              # Sales (checkout transaction), Shift (X/Z + closing),
│   │   │                              # ReceiptFormatter, HeldCart, Report, UserAdmin, Auth,
│   │   │                              # Dashboard, NumberSequence, StoreSettings
│   │   ├── Localization/AppCulture.cs # ar-EG culture, Latin digits, bidi isolation
│   │   └── DependencyInjection/       # AddSupermarketCore(...) composition root
│   ├── Supermarket.Wpf/               # ── shared WPF library (net8.0-windows)
│   │   ├── ArabicLayout.cs            # culture + RTL metadata applied once at start-up
│   │   ├── ArabicMessageBox.cs        # MessageBox with RtlReading | RightAlign
│   │   ├── Converters/                # EGP currency, enum → Arabic, bidi isolation, visibility …
│   │   └── Themes/Converters.xaml     # the single converter dictionary merged by both apps
│   ├── Supermarket.Admin/             # ── WPF admin shell (net8.0-windows)
│   │   ├── Themes/                    # Colors.xaml + Controls.xaml (RTL theme)
│   │   ├── Services/                  # AuthSession, DialogService, NavigationService
│   │   ├── ViewModels/                # Login, Main, Dashboard, Products, ProductEdit,
│   │   │                              # StockMovements, StockAdjustment, Catalog,
│   │   │                              # Suppliers, SupplierEdit, LowStock,
│   │   │                              # SalesAnalytics, Users, UserEdit, Permissions, Settings
│   │   └── Views/                     # LoginWindow, MainWindow, DashboardView, ProductsView,
│   │                                  # ProductEditWindow, StockMovementsView, StockAdjustmentWindow,
│   │                                  # CatalogView, SuppliersView, SupplierEditWindow,
│   │                                  # SalesAnalyticsView (LiveCharts), UsersView,
│   │                                  # UserEditWindow, PermissionsWindow, SettingsView
│   └── Supermarket.POS/               # ── WPF point of sale (net8.0-windows)
│       ├── Themes/PosTheme.xaml       # light touch-friendly RTL theme (admin palette)
│       ├── Printing/                  # EscPosImageBuilder (WPF typesetting → 1 bpp raster),
│       │                              # RawPrinterHelper (winspool RAW), ThermalPrinterService
│       ├── Services/DialogService.cs  # RTL message boxes + text prompt
│       ├── ViewModels/                # Login, Sales, CartLine, Payment, ShiftClose
│       └── Views/                     # LoginWindow, SalesWindow, PaymentWindow,
│                                      # ShiftCloseWindow, HeldCartsWindow, InputDialog
├── tests/
│   └── Supermarket.Core.Tests/        # 87 xUnit tests (SQLite in-memory harness)
├── tools/
│   └── Supermarket.Migrations/        # design-time host for `dotnet ef`
├── database/
│   └── SupermarketDb_Schema.sql       # idempotent SQL Server schema + seed script
└── docs/                              # architecture, database setup, roadmap
```

## 2. Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 8.0 (8.0.400+ recommended) |
| SQL Server | 2019 / 2022 (Express or Developer) |
| OS for the WPF apps | Windows 10 (build 19041+) / 11 — required by the SkiaSharp/LiveCharts WPF assets |
| EF Core CLI (optional) | `dotnet tool install --global dotnet-ef --version 8.0.33` |

## 3. Quick start

```bash
# 1. restore & build everything
dotnet build SupermarketSystem.sln

# 2. point the applications at your SQL Server instance
#    (edit src/Supermarket.Admin/appsettings.json and src/Supermarket.POS/appsettings.json)
#    default: Server=.\SQLEXPRESS;Database=SupermarketDb;Trusted_Connection=True

# 3. create / migrate the database
dotnet ef database update --project src/Supermarket.Core --startup-project tools/Supermarket.Migrations
#    ... or simply execute database/SupermarketDb_Schema.sql in SSMS

# 4. run the tests
dotnet test tests/Supermarket.Core.Tests

# 5. launch the applications
dotnet run --project src/Supermarket.Admin
dotnet run --project src/Supermarket.POS
```

### Default accounts (seeded)

| User name | Password | PIN | Role |
|---|---|---|---|
| `admin` | `Admin@123` | `1111` | مدير النظام (Administrator) |
| `manager` | `Manager@123` | `3333` | مشرف (Manager) |
| `cashier` | `Cashier@123` | `2222` | كاشير (Cashier) |
| `stockkeeper` | `Stock@123` | `4444` | أمين مخزن (StockKeeper) |

Passwords and PINs are PBKDF2 hashes (`PasswordHasher<User>`); the plain text above only documents
the seeded values so the solution can be tried immediately.

## 4. Roadmap status

| Phase | Scope | Status |
|---|---|---|
| **1** | Core architecture & database (entities, Fluent API, migrations, seed, shared services, admin + POS shells) | ✅ **Delivered** |
| **2** | Admin – product & inventory management (grid, search, low-stock alerts, forms, stock adjustment, movement history, categories & units) | ✅ **Delivered** |
| **3** | POS – touch & keyboard sales screen, barcode auto-focus, cart, F-key shortcuts | ✅ **Delivered** |
| **4** | POS – payment dialog, 80 mm thermal printing (shaped Arabic), X/Z shift closing, hold / resume cart, POS light theme | ✅ **Delivered** |
| **5** | Admin – LiveCharts analytics, invoice browser + CSV export, store settings, roles & per-user permissions | ✅ **Delivered** |

## 5. Documentation

* [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — layers, data model, ledger & audit design, DI lifetimes.
* [`docs/DATABASE-SETUP.md`](docs/DATABASE-SETUP.md) — connection strings, migrations, SQL script, troubleshooting.
* [`docs/PHASE-1-DELIVERY.md`](docs/PHASE-1-DELIVERY.md) — file-by-file inventory of the first drop.
* [`docs/PHASE-3-DELIVERY.md`](docs/PHASE-3-DELIVERY.md) — sales screen, cart pricing, keyboard map.
* [`docs/PHASE-4-DELIVERY.md`](docs/PHASE-4-DELIVERY.md) — payment dialog, ESC/POS raster printing, X/Z reports, held carts.
* [`docs/PHASE-5-DELIVERY.md`](docs/PHASE-5-DELIVERY.md) — LiveCharts analytics, invoice browser, settings, roles & permissions.
* [`docs/OPERATIONS-GUIDE.md`](docs/OPERATIONS-GUIDE.md) — unzip / navigate / publish / how to make edits (with the verified publish commands).

## 6. POS keyboard map

| Key | Action |
|---|---|
| `F1` | Help sheet |
| `F2` | Search by name / barcode |
| `F3` | Edit the quantity of the selected line |
| `F4` | Line discount (%) |
| `F5` | **Hold (park) the current cart** |
| `Shift+F5` | **Resume a held cart** |
| `F6` | Next line |
| `F7` | Invoice discount (amount or `10%`) |
| `F8` | Delete the selected line |
| `F9` | Pay — opens the payment dialog |
| `F10` | Shift closing — X / Z report |
| `Esc` | Clear the cart |
| `+` / `-` | Increase / decrease the quantity of the selected line |

Barcode capture always goes through the barcode box + `Enter` (works with cheap USB wedge
scanners); the box re-claims the focus after every command.

## 7. Roles & permissions

Permissions are keys (e.g. `Products.Edit`, `Pos.Discount`, `Reports.View`) resolved as
**role default + per-user overrides**; only the deviations are stored in `UserPermissions`.

| Role | Default emphasis |
|---|---|
| `admin` | everything |
| `manager` | catalogue, stock, suppliers, invoices, reports, settings (view), POS sell/discount/shift close, users (view) |
| `cashier` | dashboard, products (view), invoices (view), POS sell, POS shift close |
| `stockkeeper` | products edit, catalogue (view), stock + adjustments, suppliers |

The admin sidebar hides every module the operator cannot open, and the POS gates the invoice discount
(`Pos.Discount`) and the shift closing (`Pos.ShiftClose`). The last active administrator can never be
demoted or suspended, and nobody can remove their own administration rights.

## 8. Conventions cheat-sheet

* MVVM via **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]`, `[AsyncRelayCommand]`).
* Every database call is **async** (`ToListAsync`, `CountAsync`, `SaveChangesAsync`, …) — no blocking `.Result`.
* All Arabic columns are mapped with `IsUnicode(true)` (i.e. `nvarchar`).
* Money: `decimal(18,2)` for amounts, `decimal(18,4)` for prices/costs, `decimal(18,3)` for quantities.
* Soft delete (`IsDeleted`) + query filters; the ledger (`StockMovements`) is the single source of truth for stock.

## التحقق من العرض العربي

راجع [`docs/RTL-VERIFICATION.md`](docs/RTL-VERIFICATION.md) لتقرير RTL وخطوات التحقق على ويندوز.
