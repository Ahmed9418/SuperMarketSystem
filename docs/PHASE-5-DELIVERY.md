# Phase 5 — التحليلات، التقارير، الأدوار والصلاحيات

Phase 5 turns the back office into a management tool: a LiveCharts analytics page, an invoice
browser with CSV export, a store settings screen, and a full role / permission system enforced by both
applications.

| # | Deliverable | Status |
|---|-------------|--------|
| 1 | Sales & analytics page with LiveCharts (4 visuals + KPIs) | ✅ |
| 2 | Invoice browser with detail panel, filters and CSV export | ✅ |
| 3 | Store settings screen (receipt header, printer, VAT defaults) | ✅ |
| 4 | Users, roles and the per-user permission matrix | ✅ |
| 5 | Permission enforcement in the Admin shell, the users screen and the POS | ✅ |
| 6 | Unit tests for the reporting math and the permission rules | ✅ 87 / 87 |
| 7 | Docs, schema seed and the Phase 5 snapshot | ✅ |

**No database migration was needed**: Phase 5 reuses `UserPermissions` (`nvarchar` key + `IsGranted`)
which already existed since Phase 1. `has-pending-model-changes` reports none.

---

## 1. Sales & analytics — `ViewModels/SalesAnalyticsViewModel.cs`, `Views/SalesAnalyticsView.xaml`

One period selector, four charts, three tables — all fed by a single SQL aggregation pass
(`ReportService.GetSalesAnalyticsAsync`), so a chart can never disagree with a tile.

### KPI tiles
| Tile | Content |
|------|---------|
| إجمالي المبيعات | net sales, invoice count, average invoice, growth vs the previous period of the same length |
| صافي الربح | profit (`(unit price − unit cost) × qty − discount`) and profit margin % |
| عدد الأصناف المباعة | total quantity, number of lines, total discount |
| الورديّات المقفلة والنقدية | closed shifts in the period, cash difference (عجز / زيادة), credit sales |

### Charts
| Chart | Series | Axis notes |
|-------|--------|-----------|
| حركة المبيعات والأرباح اليومية | `LineSeries<DateTimePoint>` — sales (filled) + profit (line) | one point per calendar day, empty days drawn as zero so the axis never collapses |
| المبيعات حسب طريقة الدفع | `PieSeries<double>` — نقدي / بطاقة / محفظة / آجل | legend below + a small table with count and value |
| المبيعات حسب الأقسام | `ColumnSeries<double>` — top 8 categories | Arabic category labels, rotated |
| أفضل 8 منتجات | `RowSeries<double>` — revenue per product | labels reversed so the best seller sits on top |

Charts are recreated (not mutated) on every load, which lets LiveCharts animate the transition.

### Tables
* **أداء الكاشيرين** — invoices, sales, profit, shifts, cash difference per cashier.
* **قيمة المخزون حسب القسم** — products, low-stock count, quantity, stock value at cost.
* **فواتير الفترة** — the last 100 invoices; double click opens the **invoice detail overlay**
  (lines with quantity, unit price, discount, VAT, total, profit) with the totals strip underneath.

### Export
`⬇ تصدير الفواتير (CSV)` writes `Documents/SupermarketReports/Sales-<from>-<to>.csv` as **UTF-8 with
BOM** so Excel renders the Arabic correctly. No Excel interop, no extra package.

### Filters
Period presets (`اليوم`, `أمس`, `آخر 7 أيام`, `آخر 30 يوم`, `هذا الشهر`, `الشهر الماضي`, `هذا العام`),
free from/to dates and a cashier selector. The range is expressed in **local days** and converted to
the exclusive UTC window used by every query (`ReportService.ToUtcWindow`).

## 2. Reporting service — `Services/ReportService.cs`

| Method | Use |
|--------|-----|
| `GetSalesAnalyticsAsync(ReportFilter)` | the whole analytics page in one round trip |
| `GetInvoicesAsync(InvoiceFilter)` | paged browser (search by number / customer, cashier, method, status) |
| `GetInvoiceDetailAsync(int)` | invoice header + lines (detail panel, audit, reprint basis) |
| `GetTopProductsAsync(ReportFilter, take)` | best sellers ordered by revenue |
| `GetDailySalesAsync(ReportFilter)` | the original `DailySalesDto` series for the dashboard |

Rules baked into the SQL:

* only `InvoiceStatus.Completed` rows enter a sales figure; cancelled and refunded counts are reported
  separately;
* profit is `(UnitPrice − UnitCost) × Quantity − DiscountAmount` on the invoice line snapshot, so a later
  cost change never rewrites history;
* `ExpectedCash` / `CashDifference` come from the closed `Shift` rows, never recomputed twice;
* the daily series is re-bucketed to the local day in memory (≤ 366 buckets) so a DST change cannot
  split a day in two.

## 3. Users, roles and permissions

### The catalog — `Common/AppPermissions.cs`
19 keys in 7 groups (عام، المنتجات، المخزون، الموردون، المبيعات، نقطة البيع، الإدارة) with Arabic
captions, plus the default grant set of each role:

| Role | Default emphasis |
|------|------------------|
| مدير النظام | everything |
| مشرف | catalogue, stock, suppliers, invoices, reports, settings (view), POS sell + discount + shift close, users (view) |
| كاشير | dashboard, products (view), invoices (view), POS sell, POS shift close |
| أمين مخزن | products edit, catalogue (view), stock + adjustments, suppliers |

**Design rule:** a `UserPermissions` row is written **only when a key differs from the role default**.
A missing row means "use the role default", so changing a role default in a future release
automatically applies to every user who never overrode it, and the table stays tiny.

### Service — `Services/UserAdminService.cs`
`GetAllAsync`, `GetForEditAsync`, `CreateAsync`, `UpdateAsync`, `SetActiveAsync`,
`ResetPasswordAsync`, `ResetPinAsync`, `GetPermissionsAsync`, `SavePermissionsAsync`,
`GetEffectivePermissionsAsync`.

Guard rails (enforced in the service, not just the UI):

* the **last active administrator** cannot be deactivated or demoted;
* an operator cannot suspend their own account, change their own role, or remove `Users.Manage` from
  themselves;
* user names are unique (case-insensitive check against the non-deleted rows) and cannot be blank;
* new accounts need a 6+ character password **and** a 4 digit PIN; on edit an empty box keeps the
  current secret (`PasswordHasher<User>`, PBKDF2 — the hash is never returned to the client DTO).

### Screens
| Screen | Content |
|--------|---------|
| `UsersView` | search + role filter, grid (role, phone, status, granted permissions, "مخصصة", last login) and the action bar: تعديل البيانات / الصلاحيات / كلمة المرور / الرقم السري / إيقاف‑تنشيط |
| `UserEditWindow` | full name, user name, role, phone, password + PIN (with hints that change between create and edit), active checkbox |
| `PermissionsWindow` | the matrix grouped by module, each row showing the Arabic caption, the raw key, the role default and a **مخصص / افتراضي الدور** badge, with منح الكل / سحب الكل / استعادة الافتراضي |

## 4. Enforcement

| Place | Rule |
|-------|------|
| Admin shell (`MainViewModel`) | every sidebar button is bound to a `CanView…` property fed by `AuthSession.Can(key)`; `SalesAnalyticsViewModel`, `UsersViewModel` and `SettingsViewModel` were added as real pages |
| Admin sign-in | `AuthSession.LoadPermissionsAsync` pulls the effective set right after the password check and keeps the role defaults if the read fails |
| POS sign-in | `LoginViewModel` loads the same set and hands it to `SalesViewModel.ApplyPermissions` |
| POS till | `Pos.Discount` gates F4 (line discount) and F7 (invoice discount); `Pos.ShiftClose` gates F10 (X/Z report + closing) |
| Settings screen | the save button is disabled without `Settings.Edit` |

## 5. Store settings — `Views/SettingsView.xaml`

Store name, address, phone, tax number and the receipt footer (everything printed on the 80 mm
receipt), the Windows printer name, the receipt line width (32/42/48/56/64 chars with a hint about the
dot width), the VAT rate, `PricesIncludeVat`, `AutoGenerateBarcode` and the decimal places. Saving goes
through `IStoreSettingsService.UpdateAsync`, which invalidates the cached row so the POS picks the new
values up on the next receipt.

## 6. Dependencies added

| Package | Version | Why |
|---------|---------|-----|
| `LiveChartsCore.SkiaSharpView.WPF` | 2.0.0 | the requested charting library; 2.0.0 is the release that ships a *stable* `net8.0-windows7.0` asset (2.0.x newer pulls RC SkiaSharp/OpenTK assets and prints `NU1701`). |

`Supermarket.Admin` now targets `net8.0-windows10.0.19041.0` (the TFM of the SkiaSharp WPF asset), which
is the default target of any Windows 10 build 19041+ — no runtime change for the customer.

## 7. Tests — 91 / 91

| File | Tests | Covers |
|------|-------|--------|
| `ModelAndSeedTests` | 12 | model + deterministic seed |
| `InventoryServiceTests` | 12 | movements, adjustments, low stock |
| `SalesServiceTests` | 14 | pricing, stock, payment, credit, shift counters |
| `ReceiptAndShiftClosingTests` | 15 | receipt layout, X/Z math, closing rules |
| `HeldCartServiceTests` | 8 | hold / resume, re-pricing, JSON payload |
| `ReportServiceTests` | 9 | KPI sums, period isolation, cashier filter + growth, category/product breakdown, invoice browser (paging / filters / detail), daily series alignment, filter normalisation |
| `ServiceTests` (deletes) | 4 | category delete (re-homes products to «عام»), sub-category guard, unit-in-use guard, supplier delete (unlinks products) |
| `UserAdminServiceTests` | 10 | hashing, duplicates, secret retention on edit, last-admin guard, self-lockout guard, permission matrix persistence (overrides only), effective set, permission counts, password/PIN reset |

## 8. Build & verification

```bash
export PATH=/home/user/.dotnet-sdk:$PATH
cd SupermarketSystem
dotnet build SupermarketSystem.sln          # 0 warnings / 0 errors
dotnet test tests/Supermarket.Core.Tests    # Failed: 0, Passed: 91
cd tools/Supermarket.Migrations
dotnet ef migrations has-pending-model-changes --project ../../src/Supermarket.Core --startup-project .
```

## 9. What the shop sees after signing in

| Account | Sidebar |
|---------|---------|
| `admin` | everything, including المستخدمون والصلاحيات, الإعدادات, المبيعات والتحليلات |
| `manager` | catalogue, stock, suppliers, reports, settings (read only), users (read only) — no الإعدادات save |
| `cashier` | لوحة التحكم, المنتجات (view), المبيعات والتحليلات (view only if granted), POS |
| `stockkeeper` | products, stock, suppliers; no sales reports, no POS |

## 10. Admin UI revision (after the first on-machine run)

Fixes applied once the customer ran the app on Windows:

| Issue seen on screen | Cause | Fix |
|---|---|---|
| Table headers were **dark navy text on a navy band** | the implicit `TextBlock` style set `Foreground`, and that style also hits the `TextBlock` a `ContentPresenter` generates for string content — it beat the inherited white | `TextBlock` styles (admin + POS) no longer set a colour; the `Window` style owns the default; `DataGridColumnHeader` / `DataGridCell` now carry their own templates with explicit foregrounds |
| Sidebar captions were barely visible | `SidebarMuted` `#93A3BC` on the `#0F172A` rail | raised to `#B6C4DA`; hover now also flips the caption to white |
| `المراحل القادمة` heading was left over with no items under it | stale placeholder from Phase 1 | heading + divider removed |
| No way to delete a قسم / مورد | delete was never exposed | `ICategoryService.DeleteAsync`, `ISupplierService.DeleteAsync`, `IUnitService.DeleteAsync` + danger buttons in the UI |

Delete semantics (all soft deletes — the row keeps its `IsDeleted` flag so history stays valid):

* **قسم** — refused while it is the parent of another category. Its products are moved to a
  fallback category named **«عام»**, created on demand, so no product is ever orphaned.
* **مورد** — refused while purchase orders reference it. Products keep working; only their
  `SupplierId` link is cleared.
* **وحدة قياس** — refused while products use it (the message names the count).
* All three require the corresponding *edit* permission (`Catalog.Edit` / `Suppliers.Edit`), and the
  buttons stay disabled for read-only operators.

## 11. Deliberately out of scope (future work)

Returns / refund screens, purchase orders UI, purchase receiving against a PO, multi-branch
consolidation, barcode label printing, and a permission-aware audit log viewer (the `CreatedBy` /
`UpdatedBy` columns already carry the data).
