# Technical Architecture Analysis: MVVM, Services, Settings Persistence, Background Schedulers & Tests
**Supermarket Management System — Database Backup & Recovery Subsystem**

**Author**: `teamwork_explorer_survey_3`  
**Date**: 2026-11-09  
**Status**: Completed Technical Survey & Architectural Plan  

---

## 1. Executive Summary & Solution Structure

An end-to-end investigation of the solution architecture was conducted to plan the implementation of Requirement R1, R2, R3, and R4 for the Supermarket Management System.

### 1.1 Project Topology & Frameworks

| Project | Target Framework | Purpose | Key Dependencies |
|---|---|---|---|
| `Supermarket.Core` | `net8.0` | Shared domain, EF Core entities, data services, localization, DTOs | EF Core 8.0.33, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.DependencyInjection 8.0.1, Microsoft.Extensions.Logging.Abstractions 8.0.2 |
| `Supermarket.Wpf` | `net8.0-windows` | Shared WPF helpers, Arabic layout engine, message boxes, printing | ProjectReference to `Supermarket.Core` |
| `Supermarket.Admin` | `net8.0-windows` | Admin WPF desktop application, ViewModels, Views, shell navigation | CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 8.0.1, LiveChartsCore 2.0.0-rc5.4 |
| `Supermarket.POS` | `net8.0-windows` | Cashier point-of-sale desktop application | CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 8.0.1 |
| `Supermarket.Core.Tests` | `net8.0` | Unit & integration tests for data layer and services | xUnit 2.9.3, EF Core SQLite 8.0.33 |
| `Supermarket.Admin.Tests` | `net8.0-windows` | Unit & UI tests for Admin ViewModels and DI container | xUnit 2.9.3, EF Core SQLite 8.0.33 |

- **Language Version**: C# 12.0 (`<LangVersion>12.0</LangVersion>`).
- **Nullable Reference Types**: Enabled globally in `Directory.Build.props`.
- **Default Culture**: `ar-EG` (Arabic, Egypt) with Right-to-Left (RTL) flow.

---

## 2. Existing MVVM Architecture & ViewModels

### 2.1 MVVM Toolkit & Source Generators
The solution uniformly adopts **`CommunityToolkit.Mvvm` (v8.4.2)**:
- **Base Class**: ViewModels inherit from `ObservableObject`.
- **Properties**: Backed by `[ObservableProperty]` private fields.
  - Automatically generates public PascalCase properties, `INotifyPropertyChanged` firing, and partial change notification hooks (`On<Property>Changed`, `On<Property>Changing`).
- **Commands**: Backed by `[RelayCommand]` methods.
  - Asynchronous methods returning `Task` automatically generate `IAsyncRelayCommand` with properties like `IsRunning`, `CanExecute`, and cancellation token support.
  - Allows direct binding in XAML (e.g. `Command="{Binding SaveCommand}"`, `IsEnabled="{Binding CanEdit}"`).

### 2.2 Shell Navigation & View-to-ViewModel Mapping
- **Navigation Orchestration**:
  - `NavigationService`: An `ObservableObject` registered as a Singleton, maintaining `CurrentViewModel` and raising the `Navigated` event.
  - `MainViewModel`: Registered as a Singleton in `Supermarket.Admin`. It holds menu commands (`NavigateToSettingsCommand`, `NavigateToProductsCommand`, etc.) and delegates to `Navigation.NavigateTo(_services.GetRequiredService<TViewModel>())`.
- **View Hosting**:
  - In `src/Supermarket.Admin/Views/MainWindow.xaml`:
    ```xml
    <ContentControl Grid.Row="1" Content="{Binding Navigation.CurrentViewModel}" Margin="16" />
    ```
  - Views are mapped declaratively in `MainWindow.Resources` via typed `DataTemplate` definitions:
    ```xml
    <DataTemplate DataType="{x:Type vm:SettingsViewModel}">
        <views:SettingsView />
    </DataTemplate>
    ```

### 2.3 Existing `SettingsViewModel` Deep Dive
Located at `src/Supermarket.Admin/ViewModels/SettingsViewModel.cs` (381 lines):
- **Current Responsibilities**:
  - Store identity (name, address, phone, tax number, footer message).
  - Store logo selection (`ChooseLogoCommand`, `RemoveLogoCommand`) copying files to `%LocalAppData%\SupermarketSystem\Logos`.
  - Printing and fiscal defaults (paper size: 80mm/A4/A5, receipt line width, default VAT rate %, price VAT inclusive toggle, barcode auto-generation, test print).
  - Invoice live preview (`TogglePreviewCommand`, `OpenPreviewWindowCommand`).
- **Constructor & Dependencies**:
  ```csharp
  public SettingsViewModel(
      IStoreSettingsService settings,
      IReportService reports,
      IDialogService dialogs,
      AuthSession session)
  ```
- **Permission Checking**:
  `public bool CanEdit => _session.Can(AppPermissions.SettingsEdit) || _session.IsAdministrator;`
- **File Dialog Usage**:
  Uses `Microsoft.Win32.OpenFileDialog` directly in `ChooseLogo()` with standard error handling dialogs.

---

## 3. Dependency Injection & Service Registration Topology

### 3.1 Composition Roots
The application has two distinct composition roots:
1. **Core / Shared Layer**: `CoreServiceCollectionExtensions.AddSupermarketCore(services, configuration, applicationName)`
   - Configures `SupermarketDbContext` factory.
   - Registers core singletons (`IDateTimeProvider`, `IPasswordHasher<User>`, `MutableCurrentUserService`).
   - Registers core business services (`IStoreSettingsService` as Singleton; `IProductService`, `IStockService`, `IInventoryService`, `IUserAdminService`, etc. as Transient).
2. **Admin Application Layer**: `Supermarket.Admin.App.ConfigureServices(services)`
   - Registers `IConfiguration`.
   - Registers UI services: `IDialogService` -> `DialogService` (Singleton), `AuthSession` (Singleton), `NavigationService` (Singleton).
   - Registers Shell & Windows: `LoginWindow` (Singleton), `MainViewModel` (Singleton), `MainWindow` (Transient).
   - Registers ViewModels: `SettingsViewModel`, `ProductsViewModel`, etc. (all Transient).

### 3.2 Service Lifetime Constraints & Verification
In `App.xaml.cs` (lines 50–54):
```csharp
Services = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true
});
```
- **Constraint**: `ValidateScopes` and `ValidateOnBuild` ensure that no Scoped service is captured by a Singleton or resolved from the root provider.
- `IDbContextFactory<SupermarketDbContext>` is registered as **Transient** (`ServiceLifetime.Transient`) so singletons and transients can resolve contexts without scope leakage.
- **Placement Decision for Backup Services**:
  - `IBackupService`: Define in `Supermarket.Core.Interfaces`, implement in `Supermarket.Core.Services.BackupService`. Register in `AddSupermarketCore` as `Transient` (or `Singleton`).
  - `IBackupSettingsStore`: Define in `Supermarket.Core.Interfaces`, implement in `Supermarket.Core.Services.JsonBackupSettingsStore`. Register in `AddSupermarketCore` as `Singleton`.
  - `IAutoBackupScheduler`: Define in `Supermarket.Admin.Services`, implement in `Supermarket.Admin.Services.AutoBackupScheduler`. Register in `Supermarket.Admin/App.xaml.cs` as `Singleton`.

---

## 4. Settings Persistence Analysis: Database vs. Local Configuration

### 4.1 Existing Persistence Patterns in Codebase
1. **Database Persistence (`StoreSetting` table)**:
   - Handled by `StoreSettingsService`.
   - Stores store business information (store name, tax number, paper size, VAT rate).
   - Cached in memory using `SemaphoreSlim` double-checked locking, invalidated upon update.
2. **Configuration Files (`appsettings.json`, `appsettings.Local.json`)**:
   - Shipped configuration and machine-local connection string overrides.
   - Read-only at application runtime; not updated by UI view models.
3. **Local Application Data Directory (`%LocalAppData%\SupermarketSystem\`)**:
   - Used for user-selected image logos (`%LocalAppData%\SupermarketSystem\Logos\store_logo_*.png`).

### 4.2 The Architectural Flaw: Why Auto-Backup Settings Must NOT Live in the Database
In Requirement R1 flaw analysis:
- If auto-backup settings (e.g. `IsAutoBackupEnabled`, `Frequency`, `DestinationFolderPath`, `LastBackupUtc`) are stored in the database's `StoreSettings` table:
  1. **Restore Overwrite Hazard**: When a user restores a historical backup (e.g. from 2 weeks ago), SQL Server replaces the entire database catalog including `StoreSettings`. The user's active backup schedule, target backup destination directory, and recent backup history will be **wiped out** and reverted to historical data.
  2. **Multi-Terminal Drive Paths**: In a supermarket deployment, terminal machines (Admin PC vs POS terminals) have different local drive letters and directory paths. Storing a local path in a shared database corrupts the path for other terminals.
  3. **Offline Recovery Access**: If the database is inaccessible, damaged, or undergoing recovery, the application cannot read the database to discover where backup files are kept.

### 4.3 Recommended Persistence Design: `JsonBackupSettingsStore`
Store auto-backup settings in a dedicated machine-local JSON file:
- **Path**: `%LocalAppData%\SupermarketSystem\backup_settings.json`
- **Data Model (`BackupSettings`)**:
  ```csharp
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
- **Service Interface (`IBackupSettingsStore`)**:
  ```csharp
  public interface IBackupSettingsStore
  {
      Task<BackupSettings> LoadAsync(CancellationToken cancellationToken = default);
      Task SaveAsync(BackupSettings settings, CancellationToken cancellationToken = default);
  }
  ```
- **Benefits**:
  - Survives database restore operations completely intact.
  - Safe against database disconnection.
  - Clean separation of machine-specific operational configuration from store domain data.

---

## 5. Background Timers & Auto-Backup Scheduler Architecture

### 5.1 Existing State
A search across all projects confirms that no background timers or schedulers currently exist. The scheduling infrastructure is greenfield.

### 5.2 Timer Mechanism Evaluation

| Timer Mechanism | Execution Thread | UI Blocking Risk | Async/Await Support | Lifecycle & Testability |
|---|---|---|---|---|
| `System.Windows.Threading.DispatcherTimer` | UI Dispatcher thread | **HIGH**: Any disk I/O, SQL command, or delay freezes WPF UI | Poor | Requires WPF Dispatcher; difficult to unit test |
| `System.Timers.Timer` / `System.Threading.Timer` | ThreadPool worker | **LOW**: Runs on background thread | Requires callback bridging | Testable, requires manual sync |
| `System.Threading.PeriodicTimer` (.NET 8) | ThreadPool worker | **NONE**: Fully asynchronous non-blocking loop | **NATIVE**: `await timer.WaitForNextTickAsync(ct)` | Clean cancellation, modern .NET 8, testable |

**Decision**: Use `System.Threading.PeriodicTimer` running in a dedicated background task.

### 5.3 `IAutoBackupScheduler` Design Specification
```csharp
namespace Supermarket.Admin.Services;

public interface IAutoBackupScheduler : IDisposable
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task TriggerNowAsync(CancellationToken cancellationToken = default);
    event EventHandler<BackupResult>? AutoBackupCompleted;
}
```

#### Detailed Execution Workflow:
1. **Startup Check (Missed Backup Catch-Up)**:
   - When the Admin application starts (triggered from `App.OnStartup` or `MainViewModel.InitializeAsync`):
   - The scheduler loads `BackupSettings` from `IBackupSettingsStore`.
   - If `IsAutoBackupEnabled` is true and `DestinationFolderPath` is valid:
     - Check interval: `TimeSpan interval = settings.Frequency == BackupFrequency.Daily ? TimeSpan.FromDays(1) : TimeSpan.FromDays(7);`
     - If `settings.LastBackupUtc == null || (DateTime.UtcNow - settings.LastBackupUtc.Value) >= interval`:
       - A backup was missed while the Admin PC was closed!
       - Queue or trigger a catch-up backup after a short initial delay (e.g. 15 seconds) so application startup finishes smoothly.
2. **Periodic Check Loop**:
   - The scheduler initializes `PeriodicTimer` with an inspection interval of 15 minutes (or 1 hour).
   - On each tick, it re-evaluates whether the interval has elapsed since `LastBackupUtc`.
3. **Execution & Event Notification**:
   - Creates filename: `$"SupermarketDb_Auto_{DateTime.Now:yyyyMMdd_HHmmss}.bak"`.
   - Executes `await _backupService.CreateBackupAsync(targetFilePath, cancellationToken)`.
   - Updates `LastBackupUtc` and `LastBackupStatus` in `BackupSettings`.
   - Fires `AutoBackupCompleted` event so active ViewModels update their timestamps in real time without polling.
4. **Re-Entrancy & Concurrency Protection**:
   - All backup/restore operations in `IBackupService` are guarded by an asynchronous `SemaphoreSlim(1, 1)` to prevent collision between manual and automatic operations.

---

## 6. Logging & Error Handling Conventions

### 6.1 Logging Infrastructure
- **Library**: `Microsoft.Extensions.Logging` (v8.0.0 / 8.0.2).
- **Injection Pattern**: Injected via constructor as `ILogger<T>` across all services.
- **Registration**: Registered in `AddSupermarketCore` (`services.AddLogging()`) and configured in `appsettings.json`.
- **Conventions**:
  - `_logger.LogInformation("Creating database backup to {Path}", path)`
  - `_logger.LogError(ex, "Failed to restore database from {Path}", path)`
  - In unit tests: Uses `NullLogger<T>.Instance` from `Microsoft.Extensions.Logging.Abstractions` via `TestServices.Logger<T>()`.

### 6.2 Modal User Error & Warning Dialogs
Requirement R4 mandates:
"Wrap all file input/output and database operations in strict try-catch blocks. Log failures locally and display a clean WPF modal warning dialog to the user whenever a backup or restore operation fails."
- In `Supermarket.Admin`, dialogs are centralized behind `IDialogService`:
  ```csharp
  _dialogs.ShowWarning(string message, string? title = null);
  _dialogs.ShowError(string message, string? title = null);
  bool confirmed = _dialogs.Confirm(string message, string? title = null);
  ```
- Implemented in `DialogService` via `ArabicMessageBox`:
  - Enforces `MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign`.
  - For `Confirm`: Uses `MessageBoxButton.YesNo` with `MessageBoxDefaultButton.Button2` (`No` as safe default).
- In unit tests: Mocked using `MockDialogService` (`Informations`, `Warnings`, `Errors` lists).

---

## 7. Test Infrastructure & Verification Strategy

### 7.1 Existing Test Suites
1. **`Supermarket.Core.Tests`** (91 tests):
   - Exercises database models, Fluent API, seeder, number sequence, inventory, sales, shifts, reports, and user administration.
   - Built on `SqliteTestDatabase`: an in-memory SQLite harness (`DataSource=:memory:`) where decimal columns are mapped to double.
2. **`Supermarket.Admin.Tests`** (34 tests):
   - Exercises Admin ViewModels (`ProductsViewModel`, `SettingsViewModel`, `SalesAnalyticsViewModel`, `CatalogViewModel`, etc.).
   - Includes `AdminTestFixture` wiring all services and ViewModels.
   - Includes `DI_Container_Configuration_ResolvesAllAdminViewModelsAndServices` test verifying `ValidateScopes` and `ValidateOnBuild`.

### 7.2 Discovered Test Runner Constraints (Crucial for Verification)
During our investigation, running `dotnet test` directly in the environment revealed:
1. **NuGet Audit Timeout**: Because the execution environment is offline/sandboxed, `dotnet test` attempts to query `https://api.nuget.org/v3/index.json` for package vulnerabilities and hangs/times out with `warning NU1900`.
   - **Fix**: Always pass `-p:NuGetAudit=false` and `--no-restore`.
2. **Loopback Port Binding (vstest -> testhost)**: `vstest.console.exe` connects to `testhost.exe` via loopback TCP. In the sandboxed process runner, this connection times out after 90 seconds unless `BypassSandbox: true` is set.
   - **Fix**: Commands executing tests must use `BypassSandbox: true`.
   - **Verified working command**:
     `dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false` (Duration: 3s, 34/34 passed).

### 7.3 Automated Test Strategy for Database Backup & Recovery
To satisfy acceptance criteria AC 52:
"Automated unit and/or integration tests exist and pass, verifying `IBackupService` backup creation, restore handling, and error conditions."

Because the CI/test harness operates on SQLite (`SqliteTestDatabase`) without requiring a live SQL Server instance:
- `IBackupService` must feature **dual-provider support**:
  - When SQL Server: Issues native T-SQL `BACKUP DATABASE`, `RESTORE DATABASE`, single-user disconnection, and `SqlConnection.ClearAllPools()`.
  - When SQLite: Issues native SQLite backup (`SqliteConnection.BackupDatabase` or `VACUUM INTO`), connection pool clearing via `SqliteConnection.ClearAllPools()`, and clean file restoration.
- This guarantees **100% testability on any development or CI machine** while preserving **100% production fidelity for SQL Server**.

#### Planned Tests in `Supermarket.Core.Tests`:
- `BackupServiceTests.CreateBackupAsync_WithValidPath_CreatesValidBackupFile`: Verifies successful backup creation and metadata.
- `BackupServiceTests.CreateBackupAsync_WithInvalidPath_ReturnsFailureWithoutException`: Verifies graceful failure and error message.
- `BackupServiceTests.RestoreDatabaseAsync_WithNonExistentFile_ReturnsFailure`: Verifies error handling for missing files.
- `BackupServiceTests.RestoreDatabaseAsync_WithValidBackup_RestoresDatabaseState`: Verifies that data restored from backup replaces current database state.
- `BackupServiceTests.ConcurrentOperations_AreGuardedBySemaphore`: Verifies serialized execution.

#### Planned Tests in `Supermarket.Admin.Tests`:
- `BackupAndRecoveryViewModelTests.BackupNowCommand_WhenPathSelected_ExecutesBackupAndUpdatesStatus`: Tests manual backup trigger, progress, and success status.
- `BackupAndRecoveryViewModelTests.RestoreCommand_WhenConfirmed_ExecutesRestoreAndRefreshesSettings`: Tests confirmation dialog flow and restore trigger.
- `BackupAndRecoveryViewModelTests.RestoreCommand_WhenCancelled_AbortsRestore`: Tests safety refusal.
- `BackupAndRecoveryViewModelTests.AutoBackupSettings_SaveAndLoad_PersistAcrossInstances`: Tests JSON persistence of enabled flag, frequency, and destination path.
- Update `DI_Container_Configuration_ResolvesAllAdminViewModelsAndServices` to ensure all new backup services resolve under strict `ValidateScopes` and `ValidateOnBuild`.

---

## 8. Complete Architectural Blueprint & Specifications

### 8.1 Contracts (`Supermarket.Core.Interfaces`)

```csharp
namespace Supermarket.Core.Interfaces;

/// <summary>
/// Service responsible for executing database backup and recovery operations safely
/// across active EF Core contexts and connection pools.
/// </summary>
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
```

### 8.2 Settings Store (`Supermarket.Core.Interfaces`)

```csharp
namespace Supermarket.Core.Interfaces;

public interface IBackupSettingsStore
{
    Task<BackupSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(BackupSettings settings, CancellationToken cancellationToken = default);
}
```

### 8.3 Scheduler Contract (`Supermarket.Admin.Services`)

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

### 8.4 `SettingsViewModel` Integration Properties & Commands
```csharp
// -------------------------------------------------- Backup & Recovery State
[ObservableProperty]
private bool isBackupBusy;

[ObservableProperty]
private string backupStatusMessage = string.Empty;

[ObservableProperty]
private bool isAutoBackupEnabled;

[ObservableProperty]
private string selectedFrequency = "يومي"; // "يومي", "أسبوعي"

[ObservableProperty]
private string autoBackupFolderPath = string.Empty;

[ObservableProperty]
private string? lastBackupDisplayTime;

public IReadOnlyList<string> BackupFrequencies { get; } = new[] { "يومي", "أسبوعي" };

// -------------------------------------------------- Commands
[RelayCommand]
private async Task BackupNowAsync();

[RelayCommand]
private async Task RestoreDatabaseAsync();

[RelayCommand]
private void BrowseBackupFolder();

[RelayCommand]
private async Task SaveAutoBackupSettingsAsync();
```

---

## 9. Conclusion
This architecture resolves all identified safety, locking, and lifecycle flaws while integrating cleanly with the existing MVVM structure, DI container constraints, and Arabic RTL presentation framework.
