# Requirement R1: Architectural & Safety Flaw Analysis
**Supermarket Management System — Database Backup & Recovery Subsystem**

**Author**: `teamwork_explorer_survey_1`  
**Date**: 2026-11-09  
**Status**: Completed Survey & Deep Technical Analysis  

---

## 1. Executive Summary & Existing Database Architecture

An exhaustive investigation of the Supermarket Management System solution was performed across the persistence layer (`Supermarket.Core`), WPF shells (`Supermarket.Admin`, `Supermarket.POS`), migrations tooling (`Supermarket.Migrations`), and unit test projects (`Supermarket.Core.Tests`, `Supermarket.Admin.Tests`).

### 1.1 Architecture & Database Provider Survey

| Dimension | Production Environment (`Supermarket.Admin`, `Supermarket.POS`) | Test Environment (`Supermarket.*.Tests`) |
|---|---|---|
| **Database Provider** | `Microsoft.EntityFrameworkCore.SqlServer` (v8.0.33) | `Microsoft.EntityFrameworkCore.Sqlite` (v8.0.33) |
| **Driver / Client** | `Microsoft.Data.SqlClient` (v5.1.5) | `Microsoft.Data.Sqlite` (v8.0.33) |
| **Connection Configuration** | `appsettings.json` / `appsettings.Local.json`: `Server=localhost;Database=SupermarketDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True` | In-memory dynamic: `DataSource=:memory:` (`SqliteTestDatabase.cs`) |
| **DbContext Type** | `SupermarketDbContext` (Assembly: `Supermarket.Core`) | `SqliteSupermarketDbContext` inherits `SupermarketDbContext` |
| **Migrations Assembly** | `Supermarket.Core.Data.Migrations` | Generated schema on-the-fly via `Database.EnsureCreated()` |

### 1.2 DbContext & Connection Lifecycle Details

In `CoreServiceCollectionExtensions.cs` (lines 57–76):
- `services.AddDbContextFactory<SupermarketDbContext>(..., ServiceLifetime.Transient)` registers an `IDbContextFactory<SupermarketDbContext>` with a **Transient** lifetime, deliberately avoiding pooled factory (`AddPooledDbContextFactory`) because the context constructor takes `ICurrentUserService` for audit stamping (`ActivatorUtilitiesConstructor`).
- `SupermarketDbContext` is also registered as **Transient**.
- ViewModels (`SettingsViewModel`, `ProductsViewModel`, etc.) are registered as **Transient** in `Supermarket.Admin/App.xaml.cs` (lines 89–102).
- When a page is opened via `MainViewModel.Show<TViewModel>()`, a new instance is resolved via DI and held in `NavigationService.CurrentViewModel`.
- Transient services (e.g. `ProductService`, `StockService`) hold references to `SupermarketDbContext`. Even though EF Core closes connections when queries complete, **ADO.NET Connection Pooling** (`Pooling=true`, default in `Microsoft.Data.SqlClient`) maintains live physical TCP sockets and database sessions in the background.

---

## 2. Flaw Analysis Matrix (Requirement R1)

| Area | Naive / Proposed Flawed Approach | Underlying Technical Hazard | Corrective Design Decision |
|---|---|---|---|
| **Backup Strategy** | Direct file copy (`File.Copy` of `.mdf`/`.ldf` or `.db`) | **SQL Server**: OS sharing violation (`IOException: file in use by sqlservr.exe`), dirty buffer cache, torn writes, client-server path decoupling.<br>**SQLite**: Skips uncheckpointed WAL (`-wal`), torn reads during concurrent writes. | Execute native database backup command (`BACKUP DATABASE ... WITH CHECKSUM` for SQL Server, `VACUUM INTO` / `SqliteConnection.BackupDatabase` for SQLite). |
| **Connection Locks During Restore** | Direct restore while EF Core / application is active | SQL Server error `Msg 3101: Exclusive access could not be obtained because the database is in use`. Active connections and connection pool hold sessions open. | Force single-user mode via connection to `master` database (`ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE`), execute restore, reset to `MULTI_USER`. |
| **Connection Pool Invalidation** | Restoring database without resetting client pools | ADO.NET connection pool retains stale sessions severed by the server, causing subsequent queries to crash with `SqlException: Transport-level error`. | Explicitly call `SqlConnection.ClearAllPools()` (or `SqliteConnection.ClearAllPools()`) on the client before and after restore. |
| **Auto-Backup Execution** | Synchronous timer on UI thread (`DispatcherTimer`) | Backups freezing or hanging the WPF UI thread during heavy disk I/O or network delays. | Run backup on a background `System.Threading.PeriodicTimer` or `System.Timers.Timer` wrapped in non-blocking `async/await`. |
| **Scheduler Concurrency** | Periodic trigger without re-entrancy protection | Concurrent manual and automated backup operations colliding on file access or database locks. | Protect backup operations with an asynchronous mutex (`SemaphoreSlim(1, 1)`). |
| **Settings Storage** | Storing auto-backup settings solely in database (`StoreSettings`) | Restoring a backup overwrites the auto-backup schedule and machine-local folder paths with historical or foreign data. | Persist auto-backup schedule and target folder in a local configuration file (e.g. `appsettings.Local.json` or local JSON in AppData), while optionally exposing status in UI. |
| **File Permissions** | Backing up directly to user desktop folders via SQL Server | `BACKUP DATABASE` runs under the SQL Server Service account (`NT SERVICE\MSSQLSERVER`), which lacks write access to user profile directories (OS Error 5: Access is denied). | Backup to SQL Server default / staging directory, then copy resulting `.bak` to the user's selected file path via application process. |
| **Testability Gap** | SQL Server-only implementation of `IBackupService` | Breaks unit and integration tests because the CI test suite runs on SQLite in-memory without a local SQL Server instance. | Implement provider-aware logic (Strategy or provider branching) supporting both SQL Server (production) and SQLite (tests/embedded). |

---

## 3. Deep Dive Technical Analysis

### 3.1 Flaw 1: Direct File Copying vs. Native Backup/Restore

#### The SQL Server Reality (Production)
1. **OS Sharing Lock Invalidation**:
   SQL Server opens `.mdf` and `.ldf` files using Win32 `CreateFile` with exclusive sharing flags (`FILE_SHARE_READ` is omitted). When any .NET code attempts `File.Copy("...\\SupermarketDb.mdf", targetPath)`, the Windows kernel immediately rejects the call:
   ```
   System.IO.IOException: The process cannot access the file 'C:\...\SupermarketDb.mdf'
   because it is being used by another process.
   ```
2. **Buffer Cache Inconsistency & Torn Writes**:
   SQL Server uses an asynchronous Write-Ahead Logging (WAL) architecture with an in-memory Buffer Pool. Committed transactions write immediately to the log (`.ldf`), but data pages in `.mdf` remain "dirty" in RAM until a checkpoint or lazy writer flushes them. A raw byte-by-byte file copy captures an inconsistent, corrupted snapshot that SQL Server will reject upon attach without crash recovery.
3. **Client-Server Boundary Decoupling**:
   In any production or multi-terminal deployment (Admin PC + POS terminals), SQL Server resides on a server or designated host. The client WPF application does not have file-system access to the database host's physical storage. Direct file copying is fundamentally incompatible with client-server architecture.
4. **The Correct Native Approach**:
   SQL Server provides the `BACKUP DATABASE` T-SQL statement:
   ```sql
   BACKUP DATABASE [{DatabaseName}]
   TO DISK = @BackupPath
   WITH FORMAT, INIT, NAME = @BackupName, SKIP, NOREWIND, NOUNLOAD, STATS = 10, CHECKSUM;
   ```
   *Features*:
   - Generates a self-contained, validated `.bak` file.
   - Non-blocking: readers and writers continue operating during the backup.
   - Computes page checksums (`WITH CHECKSUM`) to ensure backup integrity.

#### The SQLite Perspective (Tests & Embedded)
1. **WAL Mode Data Loss**:
   When SQLite operates in Write-Ahead Logging mode (`PRAGMA journal_mode=WAL`), all incoming writes accumulate in `<name>.db-wal`. The main `<name>.db` file is only updated periodically when checkpoints occur. Performing `File.Copy("db.sqlite", "backup.sqlite")` silently drops all uncheckpointed transactions.
2. **Torn Pages During Concurrent Writes**:
   `File.Copy` reads in 4KB/8KB buffers non-atomically. If an application writes to the SQLite database during a file copy, pages can be captured half-new and half-old, corrupting the SQLite B-tree structure (`SQLITE_CORRUPT`).
3. **The Correct Native Approach**:
   - `VACUUM INTO 'backup.db'`: Atomically flushes WAL, compacts database, and generates a standalone consistent copy.
   - SQLite Online Backup API (`SqliteConnection.BackupDatabase(dest)`): Steps through pages using shared locks without interfering with active transactions.

---

### 3.2 Flaw 2: File Locking, Connection Pools, and Active EF Core Lifecycles During Restore

#### The Connection Contention Hazard (Msg 3101)
Restoring a database replaces the underlying storage structures. SQL Server requires **exclusive, uninterrupted access** to the database catalog.
If any connection is connected to `SupermarketDb`, executing `RESTORE DATABASE [SupermarketDb] FROM DISK = @path WITH REPLACE` triggers:
```
Msg 3101, Level 16, State 1, Line 1
Exclusive access could not be obtained because the database is in use.
RESTORE DATABASE is terminating abnormally.
```

#### Why EF Core `DbContext.Dispose()` Does Not Release Connections
In `Microsoft.Data.SqlClient`, connection pooling is enabled by default. When EF Core finishes a query and calls `connection.Close()` or `context.Dispose()`, the underlying physical TCP socket and server session are **not closed**. Instead, they remain pooled in memory for future reuse.
Because `Supermarket.Admin` keeps view models and services in memory, multiple pooled connections remain active, permanently blocking any simple restore command.

#### The Correct 4-Step Restore Protocol for SQL Server
To execute a 100% reliable restore:
1. **Connect to `master` Database**:
   The restore command CANNOT be issued from a connection currently contextually inside `SupermarketDb`. The connection string must target `master`:
   ```csharp
   var masterBuilder = new SqlConnectionStringBuilder(connectionString)
   {
       InitialCatalog = "master"
   };
   ```
2. **Forcibly Terminate Active Sessions via `SINGLE_USER`**:
   Roll back any open transactions and sever all other connections immediately:
   ```sql
   ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
   ```
3. **Execute Restore**:
   ```sql
   RESTORE DATABASE [{DatabaseName}] FROM DISK = @BackupPath WITH REPLACE;
   ```
4. **Re-enable Multi-User Mode**:
   ```sql
   ALTER DATABASE [{DatabaseName}] SET MULTI_USER;
   ```
5. **Clear Client Connection Pools (`SqlConnection.ClearAllPools`)**:
   **Crucial Step**: Because `ROLLBACK IMMEDIATE` severed the server sessions, the client-side ADO.NET pool still holds references to the killed sessions. The next EF Core query would crash with `A transport-level error has occurred`.
   Executing:
   ```csharp
   SqlConnection.ClearAllPools();
   ```
   purges all cached connections, ensuring subsequent application operations establish fresh, healthy connections to the newly restored database.

---

### 3.3 Flaw 3: In-App Auto-Backup Scheduling & Lifecycle Hazards

1. **Threading & Responsiveness (R3 Compliance)**:
   - In WPF, timers that dispatch on the UI thread (`System.Windows.Threading.DispatcherTimer`) will freeze the UI if any synchronous file I/O or database roundtrip occurs.
   - Auto-backup must run asynchronously on a thread pool timer (`System.Threading.PeriodicTimer` or `System.Timers.Timer`) invoking `await BackupService.CreateBackupAsync(...)`.
2. **Concurrency & Re-entrancy Protection**:
   - If an automated backup is running and the user clicks "إنشاء نسخة احتياطية الآن" (Backup Now), or if an auto-backup interval elapses before the previous backup finishes, concurrent backups can corrupt destination files or deadlock database locks.
   - A `SemaphoreSlim(1, 1)` must wrap all backup and restore operations in `IBackupService`.
3. **Application Lifecycle & Missed Backup Handling**:
   - Unlike a background Windows Service, a WPF desktop application only runs when the user launches it.
   - If the user configures "Daily" backups and shuts down their PC at 6 PM, an in-app timer scheduled for 11 PM will never trigger.
   - **Corrective Logic**: On application startup (or when the scheduler initializes), the scheduler must inspect `LastAutoBackupUtc`. If `DateTime.UtcNow - LastAutoBackupUtc >= configuredInterval`, a backup must be scheduled or triggered promptly.
4. **Settings Persistence Isolation (Database vs. Local Config)**:
   - If auto-backup settings (target folder path, enabled flag, frequency) are stored inside the `StoreSettings` table in `SupermarketDb`, a restore operation from a 2-week-old backup will **overwrite** the user's current backup schedule and folder path.
   - Furthermore, different machines (e.g. Admin PC vs POS terminal) have different local drive layouts.
   - **Recommendation**: Store auto-backup configuration in a local JSON settings file (e.g. `%LocalAppData%\SupermarketSystem\backup_settings.json` or `appsettings.Local.json`), keeping local machine configurations independent of database restorations.

---

### 3.4 Flaw 4: Dual-Provider Testability Gap & Solution

1. **The Issue**:
   The production application runs on SQL Server. However, the existing unit and integration test suite (`Supermarket.Core.Tests`, `Supermarket.Admin.Tests`) uses SQLite in-memory (`SqliteTestDatabase.cs`) so tests can run in any environment (including GitHub Actions / CI agents) without a SQL Server instance.
   If `IBackupService` only implements SQL Server T-SQL commands, it cannot be verified in the automated test suite.
2. **The Correct Architectural Solution**:
   Implement `IBackupService` with provider detection (`db.Database.IsSqlServer()` vs `db.Database.IsSqlite()`):
   - **When SQL Server**: Executes `BACKUP DATABASE`, `RESTORE DATABASE`, `SINGLE_USER WITH ROLLBACK IMMEDIATE`, `MULTI_USER`, and `SqlConnection.ClearAllPools()`.
   - **When SQLite**: Executes `VACUUM INTO` / `SqliteConnection.BackupDatabase`, connection pool clearing via `SqliteConnection.ClearAllPools()`, and clean file replacement for tests.
   - This provides **100% production fidelity** for SQL Server while enabling **full automated unit and integration test coverage** in the existing test pipeline!

---

## 4. Recommended Architectural Specification for `IBackupService`

```csharp
public interface IBackupService
{
    /// <summary>
    /// Safely creates a verified database backup at the specified destination file path.
    /// </summary>
    Task<BackupResult> CreateBackupAsync(string destinationFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely restores the database from the specified backup file path, severing active
    /// connections and clearing connection pools.
    /// </summary>
    Task<RestoreResult> RestoreDatabaseAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a file path or directory is valid and accessible for backup operations.
    /// </summary>
    Task<bool> CanWriteToPathAsync(string destinationPath);
}

public sealed record BackupResult(bool Success, string? ErrorMessage = null, string? OutputPath = null, DateTime? TimestampUtc = null);
public sealed record RestoreResult(bool Success, string? ErrorMessage = null);
```

---

## 5. Summary of Corrective Implementation Decisions

1. **Native Backup Engine**: Use T-SQL `BACKUP DATABASE` with `CHECKSUM` in production; support SQLite native backup for unit test environments.
2. **Safe Restore with Exclusive Lock Acquisition**: Connect to `master` database, issue `SET SINGLE_USER WITH ROLLBACK IMMEDIATE`, execute `RESTORE DATABASE ... WITH REPLACE`, reset to `MULTI_USER`, and call `SqlConnection.ClearAllPools()`.
3. **Asynchronous Non-Blocking Execution**: All operations async/await offloaded to background threads.
4. **Re-entrancy Protection**: Guard all backup/restore operations with `SemaphoreSlim(1, 1)`.
5. **Scheduler Reliability**: Thread pool timer with missed-schedule detection on startup, avoiding UI thread dispatchers.
6. **Local Settings Persistence**: Store auto-backup configuration locally so database restores do not clobber scheduling or local path configs.
7. **Clean Modal Error & Warning Dialogs**: Utilize existing `IDialogService` and `ArabicMessageBox` with Arabic messages for all confirmation and failure scenarios.
