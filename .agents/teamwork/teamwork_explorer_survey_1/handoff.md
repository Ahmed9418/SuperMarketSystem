# Handoff Report: Database Architecture & Flaw Analysis (R1)

**Agent**: `teamwork_explorer_survey_1`  
**Date**: 2026-11-09  
**Handoff Type**: Hard (Task Complete)  
**Target Path**: `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_explorer_survey_1\handoff.md`  
**Detailed Analysis Path**: `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_explorer_survey_1\analysis.md`  

---

## 1. Observation

### 1.1 Database Provider and Connection Setup
- In `src/Supermarket.Core/Supermarket.Core.csproj` (lines 13–15):
  ```xml
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.33" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.33" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.33" />
  ```
- In `src/Supermarket.Admin/appsettings.json` (lines 1–5):
  ```json
  {
    "ConnectionStrings": {
      "SupermarketDb": "Server=localhost;Database=SupermarketDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
    }
  }
  ```
- In `src/Supermarket.Core/DependencyInjection/CoreServiceCollectionExtensions.cs` (lines 66–76):
  ```csharp
  services.AddDbContextFactory<SupermarketDbContext>((provider, options) =>
  {
      options.UseSqlServer(connectionString, sql =>
      {
          sql.MigrationsAssembly(typeof(SupermarketDbContext).Assembly.FullName);
          sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
          sql.CommandTimeout(60);
      });

      options.EnableDetailedErrors();
  }, ServiceLifetime.Transient);
  ```
  `SupermarketDbContext` is registered with `ServiceLifetime.Transient`.

### 1.2 Test Harness Uses In-Memory SQLite
- In `tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj` (lines 16–18):
  ```xml
  <!-- SQLite in-memory is used as a stand-in for SQL Server so the whole pipeline
       (DbContext, Fluent API, seed data, services) can be verified on any machine. -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.33" />
  ```
- In `tests/Supermarket.Core.Tests/SqliteTestDatabase.cs` (lines 35–44):
  ```csharp
  _connection = new SqliteConnection("DataSource=:memory:");
  _connection.Open();
  Context = CreateContext();
  Context.Database.EnsureCreated();
  ```

### 1.3 Active Context & Service Lifecycles
- In `src/Supermarket.Admin/App.xaml.cs` (lines 89–102):
  ViewModels (`SettingsViewModel`, `ProductsViewModel`, etc.) are registered as `Transient`.
- In `src/Supermarket.Admin/ViewModels/MainViewModel.cs` (lines 125–126):
  ```csharp
  private void Show<TViewModel>() where TViewModel : ObservableObject =>
      Navigation.NavigateTo(_services.GetRequiredService<TViewModel>());
  ```
  Each navigation resolves a new transient ViewModel instance, while previous instances remain alive until garbage collected.
- ViewModels hold transient business services (e.g., `ProductService`, `CatalogService`), which in turn hold `SupermarketDbContext`. Even though EF Core closes connections after query execution, the ADO.NET connection pool (`Microsoft.Data.SqlClient`) retains open TCP sockets and active sessions to `SupermarketDb`.

### 1.4 Existing Settings Structure & Localization
- In `src/Supermarket.Admin/Views/SettingsView.xaml`:
  The Settings view currently has sections for "Store Identity", "Printing & Fiscal", "Invoice Preview", and "System Info", but lacks a dedicated TabControl or sub-page for "Database Backup & Recovery".
- In `src/Supermarket.Admin/Services/DialogService.cs` (lines 24–43):
  `IDialogService` wraps `ArabicMessageBox` (`ShowInformation`, `ShowWarning`, `ShowError`, `Confirm`).
- In `tests/Supermarket.Admin.Tests/MockDialogService.cs`:
  `MockDialogService` captures warnings, errors, and confirmation dialogs for unit testing.

---

## 2. Logic Chain

1. **Premise 1 (Direct File Copying Failure)**:
   - *Observation 1.1* confirms the database engine is Microsoft SQL Server running under service process `sqlservr.exe`.
   - In SQL Server, `.mdf` and `.ldf` files are held with exclusive OS locks (no `FILE_SHARE_READ`). Any attempt to call `File.Copy` throws `System.IO.IOException: The process cannot access the file because it is being used by another process`.
   - Furthermore, SQL Server uses an in-memory buffer pool; committed changes are staged in memory and transaction logs before writing to data files. A direct file copy bypasses the buffer pool, producing an inconsistent or corrupted snapshot.
   - For SQLite (*Observation 1.2*), WAL mode (`-wal` / `-shm`) means direct file copying misses uncheckpointed transactions and risks torn writes.
   - *Inference*: Native backup commands (`BACKUP DATABASE` for SQL Server, `VACUUM INTO` / `SqliteConnection.BackupDatabase` for SQLite) are mandatory.

2. **Premise 2 (Restore Lock Conflict & Pool Poisoning)**:
   - *Observations 1.1 and 1.3* show multiple ViewModels and services resolve `SupermarketDbContext` using `Microsoft.Data.SqlClient` with connection pooling enabled.
   - Executing `RESTORE DATABASE [SupermarketDb] FROM DISK = ...` on an active database triggers SQL Server error `Msg 3101: Exclusive access could not be obtained because the database is in use`.
   - *Inference*: The restore procedure must:
     a) Connect to the `master` system database rather than `SupermarketDb`.
     b) Forcibly sever active connections via `ALTER DATABASE [SupermarketDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE`.
     c) Execute `RESTORE DATABASE [SupermarketDb] FROM DISK = @path WITH REPLACE`.
     d) Revert the database to `MULTI_USER` via `ALTER DATABASE [SupermarketDb] SET MULTI_USER`.
     e) Call `SqlConnection.ClearAllPools()` on the client to purge dead session handles from the ADO.NET pool.

3. **Premise 3 (In-App Auto-Backup Scheduling)**:
   - Running backups synchronously or on a `DispatcherTimer` will freeze the WPF UI thread during file I/O or network roundtrips.
   - Storing auto-backup folder paths in the database table (`StoreSettings`) creates a circular hazard: restoring an older backup overwrites current scheduling and local path configurations.
   - Multiple backup calls can overlap if a scheduled backup coincides with a manual backup.
   - *Inference*: Backups must run on a background thread pool timer (`System.Threading.PeriodicTimer` or `System.Timers.Timer`) using non-blocking `async/await`, guarded by `SemaphoreSlim(1, 1)`, and persist local auto-backup settings in a local machine configuration file.

4. **Premise 4 (Dual-Provider Testability)**:
   - *Observations 1.1 and 1.2* show production uses SQL Server while tests use SQLite in-memory.
   - Implementing `IBackupService` solely against SQL Server T-SQL commands would make automated testing impossible in the CI / SQLite harness.
   - *Inference*: `IBackupService` must detect the provider (`IsSqlServer()` vs `IsSqlite()`) to execute production T-SQL commands in SQL Server while supporting SQLite backup/restore in test fixtures.

---

## 3. Caveats

1. **SQL Server Service Account Permissions**:
   In SQL Server, `BACKUP DATABASE ... TO DISK = @path` executes under the security context of the SQL Server service account (e.g. `NT SERVICE\MSSQLSERVER`), not the logged-in Windows user. If the user selects a path in their personal user profile (e.g. `C:\Users\<Name>\Desktop\backup.bak`), SQL Server may fail with `Access is denied (OS Error 5)`. The implementation must stage backups in a service-accessible directory (or SQL Server's default backup folder) and copy to the user's chosen path.
2. **Multi-Terminal Connections**:
   In multi-terminal deployments where POS stations are actively connected, `SINGLE_USER WITH ROLLBACK IMMEDIATE` during restore will disconnect POS terminals. POS terminals should gracefully handle connection retry or notify the cashier.
3. **Automated Test Timeout in Sandbox**:
   Running `dotnet test` in this specific environment hit a 90-second vstest IPC timeout, whereas `dotnet build` succeeded cleanly for `Supermarket.Core` and `Supermarket.Wpf`. Test execution command options (`--no-restore`) must be used to avoid external NuGet service index delays.

---

## 4. Conclusion

1. **Direct file copying must be completely rejected**: It causes sharing violations (`IOException`) and torn writes on SQL Server, and WAL truncation data loss on SQLite.
2. **Native backup must be used**: Use `BACKUP DATABASE ... WITH FORMAT, INIT, CHECKSUM` for SQL Server, and `VACUUM INTO` / `SqliteConnection.BackupDatabase` for SQLite.
3. **Safe restore protocol established**:
   - Connect via `master` database.
   - `ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;`
   - `RESTORE DATABASE [{DatabaseName}] FROM DISK = @path WITH REPLACE;`
   - `ALTER DATABASE [{DatabaseName}] SET MULTI_USER;`
   - Client-side `SqlConnection.ClearAllPools()` to prevent stale connection errors.
4. **Auto-backup subsystem architecture**:
   - Background thread scheduler (`PeriodicTimer` / `Timer`).
   - `SemaphoreSlim(1, 1)` concurrency guard against overlapping runs.
   - Local JSON configuration persistence for auto-backup settings.
   - Missed-schedule detection on application startup.
5. **Dual-provider design**:
   - `IBackupService` architected with provider branching or strategy pattern to ensure 100% testability under SQLite in `Supermarket.Admin.Tests` and `Supermarket.Core.Tests`.

---

## 5. Verification Method

To independently verify the observations, logic, and architectural findings:

1. **Inspect DbContext & Provider Registrations**:
   - View `src/Supermarket.Core/DependencyInjection/CoreServiceCollectionExtensions.cs` lines 66–76 to confirm `UseSqlServer` and `ServiceLifetime.Transient`.
   - View `tests/Supermarket.Core.Tests/SqliteTestDatabase.cs` lines 33–68 to confirm SQLite in-memory configuration.
2. **Inspect Existing Settings View and ViewModel**:
   - View `src/Supermarket.Admin/Views/SettingsView.xaml` to inspect current layout.
   - View `src/Supermarket.Admin/ViewModels/SettingsViewModel.cs` to inspect injected dependencies (`IStoreSettingsService`, `IDialogService`).
3. **Inspect Flaw Analysis Document**:
   - Read the comprehensive analysis at:
     `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_explorer_survey_1\analysis.md`
4. **Build Verification**:
   - Execute:
     `dotnet build src/Supermarket.Core/Supermarket.Core.csproj --no-restore`
     `dotnet build src/Supermarket.Wpf/Supermarket.Wpf.csproj --no-restore`
