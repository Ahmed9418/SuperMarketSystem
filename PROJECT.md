# Project: Supermarket Management System — Database Backup & Recovery Subsystem

## Architecture
The Database Backup & Recovery subsystem provides production-grade, secure, and non-blocking database backup and restoration capabilities for the Supermarket Management System.
- **Data & Core Layer (`Supermarket.Core`)**:
  - `IBackupService` & `BackupService`: Native database backup and restore execution. Implements dual-provider support (SQL Server T-SQL `BACKUP DATABASE` / `RESTORE DATABASE` in production; SQLite `BackupDatabase` / `VACUUM INTO` in automated test fixtures). Controls connection pool clearing (`ClearAllPools()`) and single-user isolation (`ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE`) during restore.
  - `IBackupSettingsStore` & `JsonBackupSettingsStore`: Persists auto-backup configuration (`IsAutoBackupEnabled`, `Frequency`, `DestinationFolderPath`, `LastBackupUtc`) to a machine-local JSON file (`%LocalAppData%\SupermarketSystem\backup_settings.json`). This cleanly isolates configuration from the database so that database restore operations never wipe out local scheduling or folder paths.
- **Presentation & Scheduling Layer (`Supermarket.Admin`)**:
  - `SettingsView.xaml`: Modernized using a 2-tab `TabControl` styled via existing resources. Tab 1 houses General Settings & Printing; Tab 2 houses Database Backup & Recovery. Designed with 3 clean cards adhering to Right-to-Left (RTL) Arabic localization: Status Card, Manual Backup & Recovery Card, and Auto-Backup Card.
  - `Themes/Controls.xaml`: Native WPF `ToggleSwitchStyle` for `CheckBox` controls to provide a modern toggle switch look without introducing external third-party dependencies.
  - `SettingsViewModel.Backup.cs`: Partial ViewModel class exposing asynchronous `[RelayCommand]` methods (`BackupNowCommand`, `RestoreDatabaseCommand`, `BrowseBackupFolderCommand`, `SaveAutoBackupSettingsCommand`) and observable state properties for real-time UI feedback.
  - `IAutoBackupScheduler` & `AutoBackupScheduler`: Background scheduler utilizing .NET 8 `System.Threading.PeriodicTimer` on a ThreadPool worker. Handles missed-schedule catch-up on application launch, periodic checks, and concurrency serialization via `SemaphoreSlim(1, 1)`.
- **E2E Testing Track**:
  - Independent requirement-driven opaque-box test suite verifying Tiers 1-4 (Feature coverage, Boundary & corner cases, Cross-feature interactions, and Real-world scenarios).

---

## Architectural & Safety Flaw Analysis (R1)
A comprehensive flaw analysis was performed on the original proposed plan and database architecture:
1. **Flaw 1: Direct File Copying vs. Native Database Commands**:
   - *Issue*: Directly copying database files (`.mdf`/`.ldf` or `.db`) while active causes fatal sharing violations (`System.IO.IOException`) because SQL Server (`sqlservr.exe`) locks files exclusively. Furthermore, in-memory buffer pool pages remain dirty, leading to corrupt or torn snapshots. In SQLite, direct file copying misses uncheckpointed WAL (`-wal`) transactions.
   - *Resolution*: Direct file copying is strictly avoided. Native commands are used: `BACKUP DATABASE [{DbName}] TO DISK = @path WITH FORMAT, INIT, CHECKSUM` for SQL Server, and `SqliteConnection.BackupDatabase` / `VACUUM INTO` for SQLite.
2. **Flaw 2: File Locking & Connection Pool Poisoning during Restore**:
   - *Issue*: In an active application, open EF Core contexts and ADO.NET connection pools (`Microsoft.Data.SqlClient`) maintain open physical sockets and session locks. Calling `RESTORE DATABASE` fails with SQL Server error `Msg 3101: Exclusive access could not be obtained`.
   - *Resolution*: The restore procedure connects exclusively through the `master` system database, executes `ALTER DATABASE [{DbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE` to sever all client connections, restores the database with `REPLACE`, reverts to `SET MULTI_USER`, and invokes client-side `SqlConnection.ClearAllPools()` to purge dead connection handles from the pool.
3. **Flaw 3: Auto-Backup Settings Stored in Database (Circular Wipeout Hazard)**:
   - *Issue*: If auto-backup settings and local destination folder paths are saved inside the database (`StoreSettings` table), restoring a backup from 2 weeks ago replaces the entire database catalog, destroying the user's active backup schedule and reverting local paths.
   - *Resolution*: Auto-backup settings are persisted in a machine-local JSON file (`%LocalAppData%\SupermarketSystem\backup_settings.json`), completely decoupled from the database lifecycle.
4. **Flaw 4: UI Thread Freezing & Unprotected Re-entrancy**:
   - *Issue*: Running backups synchronously or using UI `DispatcherTimer` freezes the WPF UI. Concurrent manual and scheduled backups risk colliding.
   - *Resolution*: Background scheduling uses .NET 8 `PeriodicTimer` on a ThreadPool worker, commands use non-blocking `async/await`, and all operations in `IBackupService` are serialized using `SemaphoreSlim(1, 1)`.

---

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Flaw Analysis Report | Comprehensive technical document detailing database file locking, pooling, and native backup design | M1 | ORIGINAL_REQUEST §R1 |
| 2 | Backup Service (`IBackupService`) | Dual-provider native backup and restore engine with connection pool clearing and single-user isolation | M1 | ORIGINAL_REQUEST §R1, §R3, §R4 |
| 3 | Local Settings Store (`IBackupSettingsStore`) | Machine-local JSON persistence for auto-backup settings in `%LocalAppData%\SupermarketSystem\backup_settings.json` | M1 | ORIGINAL_REQUEST §R3 |
| 4 | Core DI Registration | Registering `IBackupService` and `IBackupSettingsStore` in `CoreServiceCollectionExtensions.cs` | M1 | ORIGINAL_REQUEST §R3 |
| 5 | Modern ToggleSwitch Style | Clean native WPF template for `CheckBox` providing modern ToggleSwitch aesthetics | M2 | ORIGINAL_REQUEST §R2 |
| 6 | Backup & Recovery Tab in SettingsView | 2-tab TabControl in `SettingsView.xaml` with 3 RTL Arabic cards (Status, Manual, Auto-Backup) | M2 | ORIGINAL_REQUEST §R2 |
| 7 | Settings ViewModel Backup Commands & State | Async commands (`BackupNowCommand`, `RestoreDatabaseCommand`, etc.) and observable properties | M2 | ORIGINAL_REQUEST §R3 |
| 8 | Dialog Confirmations & Safety Prompts | Safety confirmation before restore, SaveFileDialog for backup path, folder picker | M2 | ORIGINAL_REQUEST §R2, §R4 |
| 9 | Background Auto-Backup Scheduler | Background timer using `PeriodicTimer` with missed-schedule catch-up and event notification | M2 | ORIGINAL_REQUEST §R3 |
| 10 | Admin DI Registration | Registering `IAutoBackupScheduler` and validating container scopes in `App.xaml.cs` | M2 | ORIGINAL_REQUEST §R3 |
| 11 | Comprehensive E2E & Integration Test Suite | Opaque-box requirement-driven test suite (Tiers 1-4) and Tier 5 adversarial hardening | M3 | ORIGINAL_REQUEST Acceptance Criteria |

---

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Core Backup & Settings Subsystem | `Supermarket.Core` (Interfaces, BackupService, JsonBackupSettingsStore, Core DI, Unit Tests) | none | IN_PROGRESS |
| M2 | Admin UI, ViewModel & Scheduler Subsystem | `Supermarket.Admin` (Controls.xaml, SettingsView.xaml, SettingsViewModel.Backup.cs, AutoBackupScheduler, Admin DI) | Interface Contracts | PLANNED |
| M3 | Final Milestone: E2E Test Pass & Hardening | Verification of 100% pass on E2E test suite (Tiers 1-4) published by E2E track, plus Tier 5 hardening | M1, M2, TEST_READY.md | PLANNED |

---

## Interface Contracts

### `Supermarket.Core.Interfaces` ↔ `Supermarket.Admin`
```csharp
namespace Supermarket.Core.Interfaces;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(string destinationFilePath, CancellationToken cancellationToken = default);
    Task<RestoreResult> RestoreDatabaseAsync(string backupFilePath, CancellationToken cancellationToken = default);
    Task<bool> CanWriteToPathAsync(string destinationPath);
}

public sealed record BackupResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? OutputPath { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime? TimestampUtc { get; init; }

    public static BackupResult Succeeded(string outputPath, long fileSizeBytes, DateTime timestampUtc) =>
        new() { Success = true, OutputPath = outputPath, FileSizeBytes = fileSizeBytes, TimestampUtc = timestampUtc };

    public static BackupResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

public sealed record RestoreResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static RestoreResult Succeeded() => new() { Success = true };
    public static RestoreResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}

public interface IBackupSettingsStore
{
    Task<BackupSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(BackupSettings settings, CancellationToken cancellationToken = default);
}

public sealed class BackupSettings
{
    public bool IsAutoBackupEnabled { get; set; } = false;
    public BackupFrequency Frequency { get; set; } = BackupFrequency.Daily;
    public string? DestinationFolderPath { get; set; }
    public DateTime? LastBackupUtc { get; set; }
    public string? LastBackupStatus { get; set; }
}

public enum BackupFrequency
{
    Daily,
    Weekly
}
```

### `Supermarket.Admin.Services`
```csharp
namespace Supermarket.Admin.Services;

public interface IAutoBackupScheduler : IDisposable
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task TriggerNowAsync(CancellationToken cancellationToken = default);
    void ReloadSchedule(BackupSettings settings);
    event EventHandler<BackupResult>? AutoBackupCompleted;
}
```

---

## Code Layout
- **Core Interfaces**: `src/Supermarket.Core/Interfaces/IBackupService.cs`, `IBackupSettingsStore.cs`, `BackupModels.cs` (Owned by M1 Worker)
- **Core Services**: `src/Supermarket.Core/Services/BackupService.cs`, `JsonBackupSettingsStore.cs` (Owned by M1 Worker)
- **Core DI**: `src/Supermarket.Core/DependencyInjection/CoreServiceCollectionExtensions.cs` (Owned by M1 Worker)
- **Core Unit Tests**: `tests/Supermarket.Core.Tests/BackupServiceTests.cs` (Owned by M1 Worker / M1 Test Writer)
- **UI Themes**: `src/Supermarket.Admin/Themes/Controls.xaml` (Owned by M2 Worker)
- **Settings View**: `src/Supermarket.Admin/Views/SettingsView.xaml` (Owned by M2 Worker)
- **Settings ViewModel**: `src/Supermarket.Admin/ViewModels/SettingsViewModel.Backup.cs` (Owned by M2 Worker)
- **Admin Scheduler**: `src/Supermarket.Admin/Services/IAutoBackupScheduler.cs`, `AutoBackupScheduler.cs` (Owned by M2 Worker)
- **Admin DI**: `src/Supermarket.Admin/App.xaml.cs` (Owned by M2 Worker)
- **Admin Unit Tests**: `tests/Supermarket.Admin.Tests/BackupAndRecoveryViewModelTests.cs` (Owned by M2 Worker / M2 Test Writer)
- **E2E Test Track**: `tests/Supermarket.Admin.Tests/E2E/` or `tests/Supermarket.Core.Tests/E2E/` (Owned by E2E Testing Track)
