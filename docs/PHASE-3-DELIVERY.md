# Phase 3 delivery — POS sales screen

Compiled with `dotnet build SupermarketSystem.sln` → **0 warnings, 0 errors**.
Test suite: **48 passing** (34 from Phases 1–2 + 14 new for the checkout engine and shifts).

---

## 1. What was built

### Supermarket.Core — checkout engine

| File | Responsibility |
|---|---|
| `Services/SalesService.cs` | `ISalesService`: `PriceCartAsync`, `CompleteSaleAsync`, `GetInvoiceAsync` |
| `Services/ShiftService.cs` | `IShiftService`: `GetOpenShiftAsync`, `GetOrOpenAsync` (idempotent per cashier + terminal) |
| `DTOs/SalesDtos.cs` | added `CartPricingDto` (subtotal, discounts, VAT, net, total, quantity, line count) |
| `Interfaces/IServices.cs` | added `ISalesService`, `IShiftService` |

**`CompleteSaleAsync` is transactional** (`BeginTransactionAsync` → commit / rollback) and, inside one
unit of work, writes:

1. the `Invoice` (number from `INV-yyyyMMdd-####`, snapshot totals, payment method, shift id, cashier);
2. its `InvoiceItem` rows (Arabic product name, price **and cost** snapshotted for profit reporting);
3. the `Payment` tender row (skipped for credit sales);
4. one `StockMovement` per line through `IStockService` (`Sale`, negative quantity, `InvoiceId`,
   `ShiftId`, cashier name) so the ledger and the stock card stay complete;
5. the credit customer balance when `PaymentMethod == Credit`;
6. the live shift counters (`TotalSales`, per-method totals, `TotalDiscount`, `TotalTax`,
   `InvoiceCount`, `ExpectedCash = OpeningCash + TotalCashSales − TotalRefunds`).

**Re-pricing on commit.** The cart is *never* trusted as-is: every product is re-read from the
database, the current price/promotional price/cost/VAT rate are applied, duplicate scans are merged into
one line, and the sale is refused when the product vanished, is deactivated, is a piece product with a
fractional quantity, or when the requested quantity exceeds the on-hand stock. The Arabic error
messages are what the cashier sees.

**Guards** (all covered by tests): empty cart, unknown cashier, underpayment on cash/card/wallet,
credit sale without a registered customer, and **no open shift** (an invoice can never exist outside a
shift, otherwise the Z-report would miss the money).

**VAT model.** `StoreSetting.PricesIncludeVat` decides the math: prices already include VAT (Egyptian
retail default) → the tax is *extracted* from the payable total and an invoice-level discount reduces
the VAT proportionally; otherwise the tax is added on top.

### Supermarket.POS — the sales screen

| File | Purpose |
|---|---|
| `Views/SalesWindow.xaml(.cs)` | Three-pane RTL layout: totals + function keys (right), cart + barcode bar (centre), search + category + touch tiles (left) |
| `ViewModels/SalesViewModel.cs` | Cart, barcode processing, discounts, F-key commands, checkout, shift banner |
| `ViewModels/CartLineViewModel.cs` | Editable cart row (quantity, line discount, per-line VAT, `ExceedsStock` flag) |
| `Services/DialogService.cs` | RTL message boxes / confirm / text prompt behind `IDialogService` |
| `Views/InputDialog.xaml(.cs)` | Touch-friendly numeric prompt (quantity, discounts, opening float) |

**Keyboard-first behaviour**
* The barcode box keeps the focus permanently: every command completion, mouse click and dialog
  close routes the focus back to it (keyboard-wedge scanners need no driver).
* `Enter` in the barcode box resolves the code through `IProductService.FindByBarcodeAsync`; unknown
  codes produce an Arabic status message + beep, known codes are added or merged.
* Duplicate scans accumulate on one line; piece products increment by 1, weighted products by 0.5.

**Shortcut map (implemented, both keyboard and on-screen buttons)**

| Key | Action |
|---|---|
| `F1` | Help sheet with the full shortcut list |
| `F2` | Focus the product search box |
| `F3` | Edit the quantity of the selected line (validates stock + fractional rule) |
| `F4` | Percentage discount on the selected line |
| `F6` | Select the next cart line |
| `F7` | Invoice discount — accepts `250` (amount) or `10%` (percentage) |
| `F8` | Delete the selected line |
| `F9` | Pay / commit the sale |
| `F10` | Shift information sheet |
| `Esc` | Clear the cart (with confirmation) |
| `+` / `-` | Increase / decrease the selected line |

**Totals panel** shows المجموع الفرعي · الضريبة · الخصم · الصافي · الإجمالي المستحق in ج.م, live while
scanning, plus the item/unit count. Shift banner turns red with a "فتح ورديّة" button when the cashier
has no open shift, and the checkout command refuses to run in that state.

**Sign-in flow** is now: `LoginWindow` (PIN or password) → `SalesWindow`. The cashier's open shift is
loaded automatically; if none exists the banner offers to open one with an opening-float prompt.

## 2. Test coverage added in this phase (14 tests)

Money math (`PriceCartAsync`): line totals / VAT / totals-with-VAT, percentage discount with VAT
extraction, amount → percentage conversion.
Checkout (`CompleteSaleAsync`): invoice + items + payment + stock movements persisted and linked; change
due (المتبقي) computed; shift counters updated per payment method; duplicate scan merging; insufficient
stock rejection; empty cart / inactive product / fractional piece rejection; underpayment and
credit-without-customer rejection; credit sale updates the customer balance; missing shift refuses the
sale and writes nothing; reprint snapshot via `GetInvoiceAsync`.
Shifts (`IShiftService`): open is idempotent per cashier + terminal; negative opening float rejected.

## 3. Deliberately deferred to Phase 4

| Item | Why |
|---|---|
| Payment dialog (split cash + card, quick cash buttons, change display) | Phase 4 scope — `SaleRequestDto` already carries `PaidAmount`/`PaymentMethod`, so only the dialog is missing |
| 80 mm thermal receipt + ESC/POS raw printing | Phase 4 — `SaleResultDto`/`GetInvoiceAsync` return everything the receipt formatter needs |
| Hold / resume cart (`F5`/`F8` in the original plan) | needs a suspended-carts table; the POS shows `HeldCartCount` already |
| Shift closing / X–Z reports | Phase 4 — `Shift`, `ShiftReport` and the counters are already maintained by `SalesService` |

## 4. Try it

```bash
dotnet ef database update --project src/Supermarket.Core --startup-project tools/Supermarket.Migrations
dotnet run --project src/Supermarket.POS
# login: cashier / PIN 2222   (or admin / Admin@123)
# scan or type 6223000011111 then Enter, press F9 to pay
```

The seeded cashier already has an open shift (`SH-20260101-0001`, 500 EGP float) on POS-01, so the
screen is immediately usable.
