# Architectural & Safety Flaw Analysis
**Supermarket Management System — Database Backup & Recovery Subsystem**

## 1. Overview
This document delivers the architectural and safety flaw analysis required by Requirement R1 and Acceptance Criterion 1. It details the critical flaws identified in the original plan and existing database architecture, particularly regarding direct file copying versus native database backup/restore commands, file locking when database engines and EF Core contexts are active, connection releasing/pooling, and auto-backup scheduling and persistence.

---

## 2. Identified Flaws & Corrective Architectural Decisions

### Flaw 1: Direct File Copying vs. Native Database Backup/Restore
- **The Issue**:
  The existing application runs on Microsoft SQL Server (`Microsoft.EntityFrameworkCore.SqlServer` 8.0.33) in production. When SQL Server (`sqlservr.exe`) is running, the operating system holds exclusive read/write locks on data (`.mdf`) and log (`.ldf`) files without `FILE_SHARE_READ`. Any attempt to perform direct file copying (e.g. `File.Copy`) fails immediately with `System.IO.IOException: The process cannot access the file because it is being used by another process`.
  Moreover, SQL Server utilizes an in-memory buffer pool; committed transactions are staged in dirty memory pages and transaction logs before lazy-writing to disk. A raw file copy bypasses the buffer pool, resulting in a torn, incomplete, or corrupted snapshot.
  Similarly, in the SQLite test harness, write-ahead logging (WAL mode) stages active changes in `-wal` and `-shm` files; copying the main `.db` file alone misses uncheckpointed transactions.
- **Corrective Design Decision**:
  Direct file copying is strictly rejected. The subsystem executes **native database commands**:
  - In SQL Server: `BACKUP DATABASE [{DbName}] TO DISK = @path WITH FORMAT, INIT, CHECKSUM`. This instructs the SQL Server database engine itself to flush buffers, write a consistent transactional snapshot, and calculate cryptographic checksums.
  - In SQLite: Native SQLite backup API (`SqliteConnection.BackupDatabase`) or `VACUUM INTO @path`, which safely locks the database, checkpoints WAL pages, and outputs a clean standalone database file.

---

### Flaw 2: File Locking & Connection Pool Poisoning During Database Restore
- **The Issue**:
  During normal operation, the Supermarket Management System maintains active services (`CatalogService`, `SalesService`, `ProductService`, etc.) resolved through transient ViewModels. Furthermore, ADO.NET (`Microsoft.Data.SqlClient`) maintains an active client-side connection pool with open TCP sockets and server sessions.
  If an administrator triggers a database restore while connections are pooled or active, SQL Server aborts the command with:
  `Msg 3101, Level 16: Exclusive access could not be obtained because the database is in use.`
  Attempting to restore over an open connection without proper pool termination leaves the database in an inaccessible recovery state (`RESTORING` or `SUSPECT`), and client queries on poisoned connection handles throw broken connection exceptions.
- **Corrective Design Decision**:
  A deterministic, 5-step safe restore protocol is implemented in `IBackupService`:
  1. **Connect via System Database**: The restore connection is established against the `master` system database rather than `SupermarketDb`.
  2. **Sever Active Connections**: Execute `ALTER DATABASE [{DbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;` to immediately terminate any pending transactions and disconnect active client connections.
  3. **Execute Native Restore**: Execute `RESTORE DATABASE [{DbName}] FROM DISK = @path WITH REPLACE;`.
  4. **Reset Multi-User Access**: Execute `ALTER DATABASE [{DbName}] SET MULTI_USER;` to reopen the database for application traffic.
  5. **Clear Client Connection Pools**: Call `SqlConnection.ClearAllPools()` (or `SqliteConnection.ClearAllPools()`) on the client side to purge dead session descriptors from the ADO.NET connection pool.

---

### Flaw 3: Storing Auto-Backup Settings Inside the Database (Circular Wipeout Hazard)
- **The Issue**:
  In the existing codebase, store settings are stored in the `StoreSettings` table via `StoreSettingsService`. If auto-backup configuration (`IsAutoBackupEnabled`, `Frequency`, `DestinationFolderPath`, `LastBackupUtc`) is placed in this database table:
  1. **Restore Overwrite Hazard**: When an administrator restores a backup taken two weeks prior, SQL Server restores the entire database catalog, completely overwriting the `StoreSettings` table. Consequently, the user's current backup schedule, recent backup history, and local backup destination folder are wiped out and reverted to the historical state.
  2. **Multi-Terminal Drive Incompatibilities**: In a supermarket deployment, terminal machines (Admin PC vs POS terminals) have different local drive letters and directory paths. Storing a local path in a shared database corrupts the path for other terminals.
  3. **Disaster Recovery Blindness**: If the database is corrupt or offline, the application cannot read the database to locate where backup files are saved.
- **Corrective Design Decision**:
  Auto-backup configuration is persisted outside the database in a machine-local JSON file:
  `%LocalAppData%\SupermarketSystem\backup_settings.json` via `IBackupSettingsStore` (`JsonBackupSettingsStore`). This guarantees that configuration survives database restores, remains accessible offline, and accommodates machine-specific paths.

---

### Flaw 4: UI Thread Freezing & Scheduler Re-Entrancy
- **The Issue**:
  Executing backup or restore commands synchronously or utilizing WPF `DispatcherTimer` executes disk I/O, network transfers, and SQL queries directly on the WPF UI thread, freezing the user interface and causing Windows to flag the application as "Not Responding".
  Additionally, without re-entrancy protection, a scheduled auto-backup could trigger while a manual backup or restore is in progress, corrupting backup outputs.
- **Corrective Design Decision**:
  - All operations in `IBackupService` and `SettingsViewModel` use non-blocking `async/await` (`Task.Run` / async ADO.NET methods).
  - Background auto-backup scheduling is implemented via `IAutoBackupScheduler` using .NET 8 `System.Threading.PeriodicTimer` on ThreadPool background workers.
  - Missed backups (e.g. PC was shut down during scheduled backup time) are detected upon application launch and executed after a smooth startup delay.
  - All backup and restore executions are strictly serialized using an asynchronous `SemaphoreSlim(1, 1)` concurrency gate.

---

## 3. Acceptance Verification Summary
| Flaw Area | Pre-Analysis State | Implemented Architecture & Safety Measure |
|---|---|---|
| Database Engine Backup | Direct file copy | Native `BACKUP DATABASE` / `SqliteConnection.BackupDatabase` |
| Restore Exclusivity | File-in-use crash | Master DB + `SINGLE_USER WITH ROLLBACK IMMEDIATE` + `ClearAllPools()` |
| Settings Storage | DB table overwrite risk | Independent local JSON `%LocalAppData%\SupermarketSystem\backup_settings.json` |
| Execution Threading | UI lockup risk | `async/await` + `PeriodicTimer` ThreadPool scheduler + `SemaphoreSlim(1, 1)` |
