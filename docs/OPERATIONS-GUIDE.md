# دليل التشغيل والتطوير — Operations & Developer Guide

Everything you need after the five phases: **unzip → run → publish → edit**.
Every command below was executed and verified on this snapshot (Linux build agent, .NET SDK 8.0.427);
the two WPF applications are Windows-only, so the *run* steps happen on Windows 10/11 (build 19041+).

---

## 0. Revision summary — what is actually inside the box

| Metric | Value |
|---|---|
| Projects | 5 (`Core`, `Admin`, `POS`, `Core.Tests`, `Migrations` host) |
| C# files / lines | 127 / ≈ 28 400 |
| XAML files / lines | 28 / ≈ 4 000 |
| xUnit tests | **87 passed, 0 failed** |
| Build | **0 errors, 0 warnings** (`-warnaserror` clean on a full rebuild) |
| Migrations | 2 (`InitialCreate`, `AddHeldCarts`) → **21 tables** |
| DTOs / services | 7 DTO files, 17 services |
| TODO / placeholder / not-implemented | **0** in production code |

### Phase by phase
| Phase | Delivered |
|---|---|
| 1 | Core architecture: 19 persisted entities (20 classes with `UserPermission`), Fluent API, audit stamping, soft delete, deterministic seed, shared services, both WPF shells, idempotent SQL script |
| 2 | Admin catalog: products grid + editor, low stock, stock movements, stock adjustment, categories, units, suppliers |
| 3 | POS sales screen: barcode box + Enter, touch tiles, cart, quantities/discounts, F-keys, cart pricing |
| 4 | Payment dialog (cash/card/wallet/credit), 80 mm ESC/POS **raster** printing with shaped Arabic, X/Z shift closing, hold/resume cart, POS light theme |
| 5 | LiveCharts analytics (4 charts + KPIs), invoice browser + detail + CSV export, store settings, users/roles/permission matrix enforced in both apps |

### Verification performed for this revision
| Check | Result |
|---|---|
| `dotnet build SupermarketSystem.sln --no-incremental` | ✅ 0 warnings / 0 errors |
| `dotnet test tests/Supermarket.Core.Tests` | ✅ 87 / 87 |
| `dotnet ef migrations has-pending-model-changes` | ✅ none |
| `dotnet publish` POS framework-dependent (`win-x64`) | ✅ 52 files / 13 MB, `appsettings.json` copied |
| `dotnet publish` POS self-contained single file | ✅ `Supermarket.POS.exe` 151 MB |
| `dotnet publish` Admin self-contained single file | ✅ `Supermarket.Admin.exe` 193 MB |
| XAML `StaticResource` keys (both apps) | ✅ 0 missing keys |
| `TODO/FIXME/NotImplementedException` | ✅ none (only the 4 intentional `ConvertBack` throws of one-way converters) |
| Arabic/RTL/`IsUnicode(true)`/`ج.م` conventions | ✅ spot-checked across views, entities and receipts |

**One real defect found and fixed during this revision:** the printer name maintained on the admin
**settings screen** (`StoreSetting.ReceiptPrinterName`) was stored but never read by the POS, because
the till only looked at `Pos:PrinterName` in its own `appsettings.json`. The resolution order is now
`appsettings.json → StoreSetting.ReceiptPrinterName → Windows default printer`
(`ThermalPrinterService.ResolveFromStoreSettingsAsync`). Build + publish re-verified after the fix.

### Known limitations (by design, documented, not bugs)
1. The WPF apps **cannot be executed on this Linux agent** — they compile and publish here, but the UI and
   the thermal printer must be validated on a Windows till.
2. `PlatformNotSupportedException` on **Linux/macOS** for `GS v 0` printing: the spooler calls go through
   `winspool.drv`; on another OS use the on-screen receipt preview.
3. Printing is **outside** the checkout transaction: invoice committed first, then print. A printer
   failure never loses a sale.
4. Out of scope for the 5 phases (documented backlog): returns/refunds screens, purchase orders UI,
   purchase receiving, barcode label printing, multi-branch consolidation, audit-log viewer.

---

## 1. Unzip and where everything lives

```text
SupermarketSystem-Phase5.zip
└── SupermarketSystem/
    ├── SupermarketSystem.sln        ← open this in Visual Studio
    ├── Directory.Build.props        ← shared compiler settings (C# 12, nullable, ar-EG)
    ├── README.md                    ← overview, accounts, shortcuts, roadmap
    ├── src/
    │   ├── Supermarket.Core/        ← shared library (no UI): entities, DbContext, services, DTOs
    │   ├── Supermarket.Admin/       ← WPF back office
    │   └── Supermarket.POS/         ← WPF point of sale
    ├── tests/Supermarket.Core.Tests/← 87 xUnit tests (SQLite in-memory)
    ├── tools/Supermarket.Migrations/← design-time host for `dotnet ef`
    ├── database/SupermarketDb_Schema.sql  ← 1 320-line idempotent script + seed (21 tables)
    └── docs/                        ← ARCHITECTURE, DATABASE-SETUP, PHASE-1/3/4/5-DELIVERY, this guide
```

> **Always extract a fresh copy.** Each snapshot supersedes the previous one; unzipping a new phase on
> top of an older folder leaves stale files behind. Extract to a **short path without spaces**, e.g.
> `D:\SupermarketSystem` (long paths break WPF `obj` generation on some machines).

### Open in Visual Studio
1. `SupermarketSystem.sln` → double click (VS 2022 17.8 or newer).
2. Right-click the solution → **Restore NuGet Packages** (or it restores automatically on first build).
3. Set the startup project: **Supermarket.Admin** for the back office, **Supermarket.POS** for the till
   (or right-click solution → *Configure Startup Projects* → both, if you have two monitors).

### Open in VS Code (no full IDE needed)
```bash
code D:\SupermarketSystem
dotnet build SupermarketSystem.sln
```

### First run, in order (Windows)
```powershell
# 1. database (either option)
#    a) from the script
sqlcmd -S .\SQLEXPRESS -E -i database\SupermarketDb_Schema.sql -f 65001
#    b) or with EF Core
dotnet tool install --global dotnet-ef --version 8.0.33
dotnet ef database update --project src\Supermarket.Core --startup-project tools\Supermarket.Migrations

# 2. connection string for BOTH apps
notepad src\Supermarket.Admin\appsettings.json   # ConnectionStrings:SupermarketDb
notepad src\Supermarket.POS\appsettings.json     # same string + Pos:PrinterName

# 3. sign in
dotnet run --project src\Supermarket.Admin       # admin / Admin@123
dotnet run --project src\Supermarket.POS         # cashier / Cashier@123  (PIN 2222)
```

---

## 2. Navigate the code (what to open for what)

| I want to change… | Open |
|---|---|
| a database table / column | `src/Supermarket.Core/Entities/*.cs` + `Data/Configurations/*Configuration.cs` |
| seed rows (users, products, settings) | `src/Supermarket.Core/Data/Seed/SeedData.cs` |
| business rule (pricing, stock, shift math) | `src/Supermarket.Core/Services/` |
| a screen's behaviour | `src/Supermarket.Admin/ViewModels/…` or `src/Supermarket.POS/ViewModels/…` |
| a screen's layout/colours | the matching `Views/…View.xaml`, palette in `Themes/Colors.xaml` (Admin) / `PosTheme.xaml` (POS) |
| Arabic wording of a screen | the `.xaml` (labels) and the view model messages; shared culture/bidi helpers in `Core/Localization/AppCulture.cs` |
| receipt layout (80 mm) | `Core/Services/ReceiptFormatter.cs` (text) + `POS/Printing/` (raster) |
| permission keys and role defaults | `Core/Common/AppPermissions.cs` |
| analytics / reports numbers | `Core/Services/ReportService.cs` |
| DI registration (add a new service) | `Core/DependencyInjection/CoreServiceCollectionExtensions.cs`, `Admin/App.xaml.cs`, `POS/App.xaml.cs` |
| keyboard shortcuts (POS) | `POS/Views/SalesWindow.xaml.cs` → `OnPreviewKeyDown` |

**Layer rule that keeps the code clean:** `View (xaml)` → `ViewModel` → `Interface` → `Service` → `DbContext`.
A view model never touches EF, a service never references WPF, and a view never contains logic.

---

## 3. Publish for the shop

Two applications = two publish outputs. Both were executed successfully on this snapshot.

### 3.1 Recommended: framework-dependent (small, needs the .NET 8 Desktop Runtime)
```powershell
cd D:\SupermarketSystem

dotnet publish src\Supermarket.Admin\Supermarket.Admin.csproj -c Release -r win-x64 --self-contained false -o publish\Admin
dotnet publish src\Supermarket.POS\Supermarket.POS.csproj     -c Release -r win-x64 --self-contained false -o publish\POS
```
Result: `publish\Admin\Supermarket.Admin.exe` and `publish\POS\Supermarket.POS.exe` with ~50 files each
(13 MB). Install the **.NET 8 Desktop Runtime (x64)** on each till once:
<https://dotnet.microsoft.com/download/dotnet/8.0> → *Desktop Runtime*.

### 3.2 Single file, no runtime needed (easiest to hand over on a USB stick)
```powershell
dotnet publish src\Supermarket.Admin\Supermarket.Admin.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\Admin-standalone
dotnet publish src\Supermarket.POS\Supermarket.POS.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\POS-standalone
```
Result: one `Supermarket.Admin.exe` (**193 MB**) and one `Supermarket.POS.exe` (**151 MB**) — the size is
the .NET runtime + SkiaSharp baked in. Copy the matching `appsettings.json` next to each `.exe` (the
publish step already does that).

### 3.3 Checklist after publishing
1. `appsettings.json` next to the `.exe` → correct `ConnectionStrings:SupermarketDb`.
2. POS: `Pos:PrinterName` = the **exact** Windows printer name (*Settings → Printers → name*), or leave it
   empty and type the name on the admin **الإعدادات** screen instead.
3. Run the app once → `🖨 اختبار الطابعة` on the sales screen to validate Arabic shaping/alignment.
4. Create a desktop shortcut to each `.exe`; the POS one may be pinned to the taskbar.
5. Optional: `-p:ReadyToRun=true` in the publish command for a faster first start (adds size).

---

## 4. Making edits — recipes

### 4.1 Golden loop
```bash
dotnet build SupermarketSystem.sln          # must stay 0 warnings
dotnet test tests/Supermarket.Core.Tests    # must stay 87/87 (or more)
```

### 4.2 Add a column to an existing table
1. Add the property to the entity (`Core/Entities/…`).
2. Map it (length / `IsUnicode(true)` for Arabic / precision) in the matching `…Configuration.cs`.
3. Expose it in the DTO + service if the UI needs it.
4. Create the migration:
   ```bash
   cd tools/Supermarket.Migrations
   dotnet ef migrations add AddXyz --project ../../src/Supermarket.Core --startup-project .
   dotnet ef migrations has-pending-model-changes --project ../../src/Supermarket.Core --startup-project .
   ```
5. Regenerate the SQL script (keeps SSMS users in sync, idempotent):
   ```bash
   dotnet ef migrations script --idempotent --project ../../src/Supermarket.Core --startup-project . `
     --output ../../database/SupermarketDb_Schema.sql
   ```
6. If the change must ship with data, append an `IF NOT EXISTS … INSERT` block at the end of the script
   (the pattern used for the Phase 5 permissions).

### 4.3 Add a new screen (Admin)
1. `ViewModels/MyThingViewModel.cs` — `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, async
   service calls only.
2. `Views/MyThingView.xaml` — `FlowDirection="RightToLeft"`, `{StaticResource CardBorderStyle}`,
   Arabic labels, **ج.م** on money.
3. `Views/MyThingView.xaml.cs` — `InitializeComponent()` only.
4. `Views/MainWindow.xaml` — add a `DataTemplate` (`vm:MyThingViewModel` → `views:MyThingView`) and a sidebar
   button bound to `NavigateToMyThingCommand` + `Visibility="{Binding CanViewMyThing, …}"`.
5. `ViewModels/MainViewModel.cs` — the `NavigateTo…` command, the `CurrentPageTitle` case and the
   `CanView…` property (`_session.Can(AppPermissions.MyThingView)`).
6. `App.xaml.cs` — `services.AddTransient<MyThingViewModel>();`
7. Permission key in `Core/Common/AppPermissions.cs` (catalog + role defaults).

### 4.4 Add a new business rule
1. Method on the interface in `Core/Interfaces/`.
2. Implementation in `Core/Services/` — async EF only, aggregate in SQL, throw `InvalidOperationException`
   with an **Arabic** message for user-facing failures.
3. Add a test in `tests/Supermarket.Core.Tests/` (SQLite harness; remember `decimal` is mapped to `double`
   there, and that prices are re-read from the DB).

### 4.5 Change the receipt layout
`Core/Services/ReceiptFormatter.cs` — keep every line ≤ `width` (use `PadLine` / `Center` / `Wrap`), then
update `ReceiptAndShiftClosingTests` (it asserts the width of every emitted line). Printing needs no change:
the raster builder typesets whatever the formatter returns.

### 4.6 Change colours / theme
* **Admin:** `Themes/Colors.xaml` (brushes) and `Themes/Controls.xaml` (control styles).
* **POS:** `Themes/PosTheme.xaml` — surfaces, `PosAccentBrush`, `PosBorderBrush`, plus touch sizes.
  Never hard-code a hex in a view; add a key to the theme.

### 4.7 Add a permission
`Core/Common/AppPermissions.cs`: add the `const`, a row in `Catalog` (key, Arabic caption, group) and the
default in `DefaultsFor(role)`. Nothing else is needed — the permission matrix screen is data-driven and the
admin shell reads `AuthSession.Can(key)`.

### 4.8 Common traps (learned the hard way in this project)
| Trap | Rule |
|---|---|
| XAML comments | `--` is illegal inside `<!-- -->` (MC3000) — use `=` runs |
| Resource keys | a typo only fails at runtime — grep the key before renaming |
| Arabic columns | always `IsUnicode(true)` (nvarchar), otherwise the data becomes `?` |
| Money | `decimal(18,2)`, prices `decimal(18,4)`, quantities `decimal(18,3)` |
| Seed changes | `HasData` is deterministic only with fixed ids and fixed PBKDF2 constants |
| Tests | `CompleteSaleAsync` re-prices from the DB — never assert an invented price |
| Codegen names | `[RelayCommand] SearchProductsAsync` → `SearchProductsCommand` |
| Sync command | changing an async command to sync breaks `ExecuteAsync` callers |
| Long paths | extract to `D:\SupermarketSystem`, not to a deep `Downloads\…` folder |
| DI registration | every window / view model must be registered in `App.xaml.cs`; the containers now run with `ValidateOnBuild`, so a missing one fails at F5 with the type name instead of a message box later |
| DbContext constructors | keep exactly **one** public constructor (`DbContextOptions` + optional `ICurrentUserService`); a second one breaks `AddDbContextFactory` at run time |

### 4.9 Version control (optional but recommended)
```bash
cd D:\SupermarketSystem
git init
git add .
git commit -m "Phase 5 baseline"
```
`.gitignore` already excludes `bin/`, `obj/`, `publish/`, `.vs/` and the local `appsettings.json`
copies you may fill with real server names.

---

## 5. Quick reference

### Accounts (seeded)
| User | Password | PIN | Role |
|---|---|---|---|
| `admin` | `Admin@123` | `1111` | مدير النظام |
| `manager` | `Manager@123` | `3333` | مشرف |
| `cashier` | `Cashier@123` | `2222` | كاشير |
| `stockkeeper` | `Stock@123` | `4444` | أمين مخزن |

### POS keys
`F1` help · `F2` search · `F3` quantity · `F4` line discount · `F5` hold cart · `Shift+F5` resume ·
`F6` next line · `F7` invoice discount · `F8` delete line · `F9` pay · `F10` shift closing · `Esc` clear.

### Commands
```bash
dotnet build SupermarketSystem.sln                              # 0 warnings expected
dotnet test tests/Supermarket.Core.Tests                        # 87 tests
dotnet run --project src/Supermarket.Admin                      # back office
dotnet run --project src/Supermarket.POS                        # till
dotnet publish … -c Release -r win-x64 …                        # see §3
cd tools/Supermarket.Migrations && dotnet ef migrations add Name --project ../../src/Supermarket.Core --startup-project .
```
