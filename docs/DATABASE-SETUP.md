# Database setup — SQL Server

The whole system runs on one database (`SupermarketDb`) shared by the Admin and the POS executables.

## 0. The database name

The name is **`SupermarketDb`** — exactly as written in `ConnectionStrings:SupermarketDb` in both
`appsettings.json` files. If you prefer another name, create it yourself and change the two
connection strings; nothing else refers to the name.

How to see what exists on your instance (SSMS, new query window):

```sql
SELECT name FROM sys.databases ORDER BY name;
GO
```

## 1. Connection string

Both applications read `ConnectionStrings:SupermarketDb` from their `appsettings.json`.

**SQL Server Express, Windows authentication (default)**

```json
"SupermarketDb": "Server=.\\SQLEXPRESS;Database=SupermarketDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

**SQL Server with a SQL login**

```json
"SupermarketDb": "Server=192.168.1.10,1433;Database=SupermarketDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

`AddSupermarketCore` automatically appends `Application Name=Supermarket.Admin|POS` to the connection
string, which makes the sessions distinguishable in SQL Server Profiler / Extended Events.

> `Encrypt=False` / `TrustServerCertificate=True` is required for local instances that use a self-signed
> certificate, otherwise the driver raises `A connection was successfully established … certificate chain
> was issued by an authority that is not trusted`.

## 2. Creating the schema

### Option A — EF Core migrations (recommended)

```bash
dotnet tool install --global dotnet-ef --version 8.0.33     # once

dotnet ef database update \
  --project src/Supermarket.Core \
  --startup-project tools/Supermarket.Migrations
```

The `InitialCreate` migration already contains the seed data (users, units, categories, suppliers,
25 demo products, store settings, one open shift), so the applications are usable immediately.

To regenerate a script:

```bash
dotnet ef migrations script --idempotent \
  --project src/Supermarket.Core \
  --startup-project tools/Supermarket.Migrations \
  -o database/SupermarketDb_Schema.sql
```

### Option B — plain SQL script

Open `database/SupermarketDb_Schema.sql` in SSMS / Azure Data Studio and execute it against an empty
`SupermarketDb` database. The script is idempotent (`IF NOT EXISTS` guards) and creates the tables,
indexes, foreign keys and the seed rows.

### Option C — console host

```bash
dotnet run --project tools/Supermarket.Migrations -- \
  "Server=.\SQLEXPRESS;Database=SupermarketDb;Trusted_Connection=True;TrustServerCertificate=True"
```

This applies every pending migration without the EF CLI.

## 3. What gets created

19 tables, 20 `CREATE TABLE` statements including EF's history table:

`Products`, `Categories`, `Units`, `Suppliers`, `Customers`, `StockMovements`, `PriceHistories`,
`StockCounts`, `StockCountItems`, `Invoices`, `InvoiceItems`, `Payments`, `Shifts`, `ShiftReports`,
`PurchaseOrders`, `PurchaseOrderItems`, `Users`, `UserPermissions`, `StoreSettings`,
`__EFMigrationsHistory`.

Filtered unique indexes worth knowing:

| Index | Filter | Purpose |
|---|---|---|
| `IX_Products_Barcode_Unique` | `[IsDeleted] = 0` | a barcode can be reused after a product is deleted |
| `IX_Users_UserName` | `[IsDeleted] = 0` | same for user names |
| `IX_Shifts_User_Terminal_Status` | — | fast lookup of the cashier's open shift |
| `IX_Invoices_Date`, `IX_Invoices_Shift_Status` | — | report and Z-report queries |
| `IX_StockMovements_Product_Date` | — | product stock card |

## 4. Seed data

| Table | Rows | Content |
|---|---|---|
| `Users` | 4 | admin / manager / cashier / stockkeeper (hashed passwords + PINs) |
| `Units` | 6 | قطعة، كيلوجرام، جرام، لتر، عبوة، كرتونة |
| `Categories` | 8 | ألبان وأجبان، مخبوزات، مشروبات، بقالة جافة، منظفات، لحوم ودواجن، خضار وفاكهة، مجمدات |
| `Suppliers` | 4 | النيل للألبان، مخابز الصفا، توزيع الشرق، مجموعة الدلتا |
| `Products` | 25 | Arabic names, realistic EGP prices, weighted items, two items deliberately at zero stock |
| `StoreSettings` | 1 | store header used by the thermal receipt (Phase 4) |
| `Shifts` | 1 | open shift for the cashier on `POS-01` |

## 5. Troubleshooting

| Symptom | Fix |
|---|---|
| `Login failed for user 'NT AUTHORITY\ANONYMOUS LOGON'` | enable Windows authentication for the instance or switch to a SQL login in the connection string |
| `The certificate chain was issued by an authority that is not trusted` | add `TrustServerCertificate=True` |
| `Invalid object name 'Products'` | the schema was never created — run the migration or the SQL script |
| `Database 'SupermarketDb' does not exist` | create it (`CREATE DATABASE SupermarketDb`) or let `dotnet ef database update` create it |
| Arabic text shows as `?????` | a column was created as `varchar`; the model maps Arabic columns with `IsUnicode(true)`, so re-create the table from the migration rather than by hand |
| Decimal values lose their cents | never map money to `float`; the model uses `decimal(18,2)` / `decimal(18,4)` |

## 6. Backup recommendation

Two jobs are enough for a single-store deployment:

```sql
-- nightly full backup
BACKUP DATABASE SupermarketDb
TO DISK = 'D:\Backups\SupermarketDb_FULL.bak'
WITH COMPRESSION, INIT;

-- hourly log backup (database must be in FULL recovery model)
BACKUP LOG SupermarketDb
TO DISK = 'D:\Backups\SupermarketDb_LOG.trn'
WITH COMPRESSION, INIT;
```
