# Phase 4 — الدفع، الطباعة الحرارية 80 مم، وتقفيل الورديّة

Phase 4 turns the checkout screen of Phase 3 into a complete till: take the money, print a real
Arabic 80 mm receipt, park a basket when the customer forgets something, and close the shift with an
X / Z report and a cash reconciliation.

| # | Deliverable | Status |
|---|-------------|--------|
| 1 | Payment dialog (cash / card / wallet / credit, quick denominations, keypad, change) | ✅ |
| 2 | 80 mm thermal receipt with correctly shaped Arabic (ESC/POS raster) | ✅ |
| 3 | Printer test print + on-screen fallback when no printer is attached | ✅ |
| 4 | Shift closing: X report (read only), Z report (persisted), cash reconciliation | ✅ |
| 5 | Hold / resume cart (F5 / Shift+F5) | ✅ |
| 6 | Unit tests for receipts, reporting, closing and held carts | ✅ 67 / 67 |
| 7 | Light theme for the POS matching the admin palette | ✅ |

---

## 1. Payment dialog — `Views/PaymentWindow.xaml`

`F9` on the sales screen opens it (owner window = `SalesWindow`, so the cashier cannot lose it behind
the grid).

| Area | Content |
|------|---------|
| Right panel | `الإجمالي`, `المدفوع`, `المتبقي` in large type, plus a hint line explaining that in a credit sale the full amount is booked on the customer account. |
| Method selector | `نقدي` · `بطاقة` · `محفظة إلكترونية` · `آجل` (the four values of `PaymentMethod`). |
| Quick cash | Exact amount plus the next rounded notes up to 500 ج.م; one tap fills `المدفوع`. |
| Keypad | 0-9، `00`، `٫`، `⌫`، `مسح` — the value goes into `المدفوع`, never into a masked field, so the cashier always sees the figure. |
| Credit block | Appears only for `آجل`: pick an existing customer (`العميل`) **or** type a new name; the whole amount is recorded as debt and `المدفوع` is forced to 0. |
| Buttons | `💾 حفظ وطباعة` (Enter) and `إلغاء` (Esc). |

`PaymentViewModel` computes the change locally while typing and the authoritative amount comes back
from `ISalesService.CompleteSaleAsync` (which re-prices and re-validates the cart).

Validation rules enforced before the dialog closes:

* cash / card / wallet must cover the total, otherwise `المبلغ المدفوع أقل من الإجمالي`;
* credit requires a customer (existing id or a non-empty name);
* no negative amounts; quantity / stock / shift checks stay in the sales service.

## 2. Receipt — `Services/ReceiptFormatter.cs` + `Printing/`

**Why raster.** ESC/POS text mode cannot shape Arabic: the printer's code page has isolated letter
forms only, so `لبن` prints as `ل ب ن`. The receipt is therefore typeset by WPF (which applies the
Arabic shaping and bidi algorithm) and sent to the printer as a bit image.

Pipeline:

```
ReceiptFormatter (plain Arabic text, 42/48 columns)
        │
EscPosImageBuilder  →  FormattedText(ar-EG, FlowDirection=RightToLeft, Tahoma 20pt)
        │              rendered on a 576-dot wide Pbgra32 bitmap (80 mm @ 203 dpi)
        │              FormatConvertedBitmap → BlackWhite (1 bpp, threshold)
        ▼
GS v 0 raster slices of 24 rows  →  ESC @  …  feed 4  →  GS V 1 (partial cut)
        │
RawPrinterHelper (winspool.drv: OpenPrinter / StartDocPrinter RAW / WritePrinter)
```

* Printer name comes from `Pos:PrinterName` in `appsettings.json` (`XP-80C` by default), else the
  Windows default printer, else the preview.
* Font size is `Pos:ReceiptFontSize` (default 20 pt).
* Roll width is `StoreSetting.ReceiptLineWidth` (default 42, clamped to 32–64) — the seed also supports
  a 48-column 80 mm layout.
* When no printer answers, `SalesWindow` shows the receipt text in an RTL message box instead of
  losing the sale: **the invoice is committed first, printing is never part of the transaction.**
* `🖨 اختبار الطابعة` in the sales footer prints a self-test page (store header, printer name, date,
  all four payment methods and an Arabic pangram) — used to verify alignment before the first real
  customer.

Receipt content: centred store header (name / address / phone / tax number) → invoice no, date, time,
cashier, shift → one block per line (name wrapped, `qty × price … total ج.م`, discount) → subtotal,
discount, net, VAT, total, method, paid, change / remaining → footer message + `شكراً لزيارتكم` +
item count. Every money value carries the **ج.م** suffix.

Long meta pairs (e.g. a long cashier name + shift number) are stacked onto two lines instead of
overflowing the roll — `ReceiptFormatter.PadLine` never emits a line wider than the configured width.

## 3. Shift closing — `Views/ShiftCloseWindow.xaml`, `Services/ShiftService.cs`

`F10` opens it for the cashier's open shift.

| Report | Trigger | Persisted | Content |
|--------|---------|-----------|---------|
| **X** | `تقرير X` | No — read-only snapshot while selling continues | sales block, payment split, expected cash |
| **Z** | `تقرير Z` then `تقفيل الورديّة` | `ShiftReports` row with `PrintedContent` | everything above + counted cash, difference, verdict, notes |

Sales block: invoice count, cancelled count, total sales, discount, VAT, average invoice.
Payment split: cash / card / wallet / credit + refunds.
Cash block: opening cash, expected cash, counted cash, difference, verdict (`عجز في النقدية`,
`زيادة في النقدية`, `مطابق تماماً`) and a signature area.

Reconciliation rule:

```
ExpectedCash = OpeningCash + TotalCashSales − TotalRefunds      (refunds leave the drawer)
CashDifference = ClosingCash − ExpectedCash
```

Closing is transactional: the `Shift` row is updated (status `Closed`, counters, `ClosedAtUtc`,
`ClosedBy`), a `ShiftReport` row is written, and the sales service refuses any further invoice on that
shift. The sales screen reacts through `ApplyClosedShift()`: the header switches to
`لا توجد ورديّة مفتوحة` and the cashier must open a new shift.

Validation: closing cash cannot be negative, the shift must be open, and a closed shift cannot be
closed twice (`InvalidOperationException`).

## 4. Hold / resume cart — `HeldCarts` table, `Services/HeldCartService.cs`

* `F5` — asks for a short label (customer name / note), stores the basket and clears the cart.
* `Shift+F5` (or the `↩ السلات المعلقة` button, which shows a live badge) — opens
  `Views/HeldCartsWindow.xaml` listing this cashier's parked baskets: label, line count, quantity,
  estimated value, hold time. Double click or `↩ استكمال السلة` restores it, `🗑 حذف السلة` drops it.
* The payload is stored as JSON (`HeldCartPayload`, versioned) so no schema change is needed when a
  cart line gains fields later.
* **Prices are re-read from the database on resume** — a parked basket never freezes yesterday's
  price, and a line whose stock no longer covers the quantity is flagged in the status bar instead of
  silently disappearing.
* Resuming deletes the row (a held cart can be resumed once); parked baskets are per cashier and
  never touch stock or the shift — only the final invoice does.

## 5. UI polish delivered in this phase

* **POS light theme** matching the admin palette: background `#F4F6FB`, surfaces white, primary
  `#1D4ED8`, dark navy header `#0F172A`, light blue chips `#EFF6FF`, borders `#DDE3EE` — while the
  touch sizes stay (52 px buttons, 64 px F-key chips, 46 px inputs). (Palette re-skinned to
  *Modern Blue* in Phase 5; the single source of truth is `src/Supermarket.Admin/Themes/Colors.xaml`.)
* Fixed a latent binding bug: `ShiftCloseWindow` bound `IsEnabled` to a **Visibility** converter, so
  the closing-cash box and the notes box were always disabled. Added `InverseBooleanConverter`
  (`{StaticResource InverseBool}`) and used it for the three numeric/enabled bindings.
* New function keys: `F5` hold, `Shift+F5` resume; the help sheet (F1) lists them.

## 6. Database changes

| Object | Change |
|--------|--------|
| `HeldCarts` | **New table** — `Label nvarchar(120)`, `UserId`, `ShiftId` (nullable), `TerminalId`, `CartJson nvarchar(4000)`, `LineCount`, `TotalQuantity decimal(18,3)`, `EstimatedTotal decimal(18,2)`, audit columns. Indexes on `UserId+IsDeleted` and `ShiftId`. |
| `ShiftReports` | Unchanged (already created in Phase 1); now actually written on every Z closing. |
| Migration | `20261105181105_AddHeldCarts` |
| Script | `database/SupermarketDb_Schema.sql` regenerated (`--idempotent`), 1300 lines |

`dotnet ef migrations has-pending-model-changes` → **no pending changes**.

## 7. Tests

`tests/Supermarket.Core.Tests` — 67 tests, 0 failures.

| File | Tests | Covers |
|------|-------|--------|
| `ModelAndSeedTests.cs` | 12 | model + deterministic seed |
| `InventoryServiceTests.cs` | 12 | movements, adjustments, low stock |
| `SalesServiceTests.cs` | 14 | pricing, stock, payment, credit, shift counters |
| `ReceiptAndShiftClosingTests.cs` | 15 | receipt header / totals / wrapping / width, `PadLine`, `Center`, X vs Z layout, expected-cash math, shortage & surplus, double closing, stored Z row, report + shift history queries |
| `HeldCartServiceTests.cs` | 8 | hold, generated label, empty basket rejection, resume re-pricing, short stock flag, delete, JSON round-trip & corruption |

## 8. Build & verification

```bash
export PATH=/home/user/.dotnet-sdk:$PATH
cd SupermarketSystem
dotnet build SupermarketSystem.sln          # 0 warnings / 0 errors
dotnet test tests/Supermarket.Core.Tests    # Failed: 0, Passed: 67
cd tools/Supermarket.Migrations
dotnet ef migrations has-pending-model-changes --project ../../src/Supermarket.Core --startup-project .
```

## 9. Operational setup (Windows till)

1. Install the printer driver (XP-80C / generic 80 mm ESC/POS). Set it as the default printer, or put
   its exact name in `Pos:PrinterName`.
2. Paper width 80 mm, 576 printable dots, 203 dpi. Do **not** enable "reduced size" fonts.
3. Press `🖨 اختبار الطابعة` on the sales screen and check that the Arabic joins correctly and the
   text is centred inside the roll.
4. Cut mode `GS V 1` (partial cut) is assumed; printers without a cutter simply ignore it.

## 10. Deliberately out of scope for Phase 4 (Phase 5)

Returns / refunds UI (the column `Shift.TotalRefunds` is already aggregated), admin analytics with
LiveCharts, scheduled reports, roles & permissions screens, multi-terminal shift handover screen.
