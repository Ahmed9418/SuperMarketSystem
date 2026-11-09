# Dispatch: Worker M1 (Core Backup & Settings Subsystem)

## Objective
Implement Milestone 1: Core Backup & Settings Subsystem in `Supermarket.Core` and corresponding unit tests in `Supermarket.Core.Tests`.

## Mandatory Reading
1. Read the original user request at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\ORIGINAL_REQUEST.md`
2. Read the project scope and interface contracts at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\PROJECT.md`
3. Read the flaw analysis document at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\FLAW_ANALYSIS.md`
4. Read Explorer 1 & 3 reports at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_explorer_survey_1\analysis.md`
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_explorer_survey_3\analysis.md`

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Write Ownership Boundaries
You exclusively own and may edit or create these files:
- `src/Supermarket.Core/Interfaces/IBackupService.cs`
- `src/Supermarket.Core/Interfaces/IBackupSettingsStore.cs`
- `src/Supermarket.Core/Interfaces/BackupModels.cs`
- `src/Supermarket.Core/Services/BackupService.cs`
- `src/Supermarket.Core/Services/JsonBackupSettingsStore.cs`
- `src/Supermarket.Core/DependencyInjection/CoreServiceCollectionExtensions.cs`
- `tests/Supermarket.Core.Tests/BackupServiceTests.cs`
DO NOT modify any other files.

## Technical Specifications
1. **Contracts**:
   Define `IBackupService`, `BackupResult`, `RestoreResult`, `IBackupSettingsStore`, `BackupSettings`, and `BackupFrequency` matching `PROJECT.md § Interface Contracts`.
2. **`BackupService` Implementation**:
   - Injected with `IDbContextFactory<SupermarketDbContext>`, `IConfiguration`, `ILogger<BackupService>`.
   - Concurrency guard: `private readonly SemaphoreSlim _gate = new(1, 1);`
   - Provider detection: detect whether provider is SQL Server (`IsSqlServer()`) or SQLite (`IsSqlite()`).
   - For SQL Server:
     - `CreateBackupAsync`: Execute `BACKUP DATABASE [{DbName}] TO DISK = @path WITH FORMAT, INIT, CHECKSUM`. Support staging to temp directory if user destination requires permission mediation.
     - `RestoreDatabaseAsync`:
       1. Connect to `master` database using connection string from configuration with database set to `master`.
       2. Execute `ALTER DATABASE [{DbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;`.
       3. Execute `RESTORE DATABASE [{DbName}] FROM DISK = @path WITH REPLACE;`.
       4. Execute `ALTER DATABASE [{DbName}] SET MULTI_USER;`.
       5. Call `SqlConnection.ClearAllPools()` to clear dead connection handles.
   - For SQLite:
     - `CreateBackupAsync`: Use native SQLite backup via `SqliteConnection.BackupDatabase` (or `VACUUM INTO`).
     - `RestoreDatabaseAsync`: Call `SqliteConnection.ClearAllPools()`, overwrite destination database file with backup file, and re-verify.
   - Error handling: Wrap all operations in strict try-catch blocks, log errors via `_logger.LogError`, and return `BackupResult.Failed(ex.Message)` / `RestoreResult.Failed(ex.Message)` without throwing unhandled exceptions.
3. **`JsonBackupSettingsStore` Implementation**:
   - Persist settings to `%LocalAppData%\SupermarketSystem\backup_settings.json`.
   - If file does not exist, return default settings (`IsAutoBackupEnabled = false, Frequency = Daily`).
   - Use `System.Text.Json` with indentation. Ensure thread-safe read/write.
4. **DI Registration**:
   In `CoreServiceCollectionExtensions.AddSupermarketCore`:
   - `services.AddTransient<IBackupService, BackupService>();`
   - `services.AddSingleton<IBackupSettingsStore, JsonBackupSettingsStore>();`
5. **Unit Tests in `BackupServiceTests.cs`**:
   - Write comprehensive unit tests in `Supermarket.Core.Tests`.
   - Test backup creation, verify output file exists and is non-empty.
   - Test restore, verify database state restores correctly.
   - Test error handling with invalid paths and non-existent files.
   - Test `JsonBackupSettingsStore` save and load roundtrip.

## Verification Instructions
Execute the following verification commands using `BypassSandbox: true`:
1. Build:
   `dotnet build src/Supermarket.Core/Supermarket.Core.csproj --no-restore -p:NuGetAudit=false`
2. Test:
   `dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Backup`

## Output
Write your findings and test execution logs to `handoff.md` in:
`C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_worker_m1\`
Send a completion message to the orchestrator.

## 2026-11-09T13:52:41Z
Received dispatch from parent orchestrator:
You are teamwork_worker_m1.
Implement Milestone 1 (Core Backup & Settings Subsystem in Supermarket.Core):
- IBackupService, BackupResult, RestoreResult in Supermarket.Core.Interfaces
- IBackupSettingsStore, BackupSettings, BackupFrequency in Supermarket.Core.Interfaces
- BackupService implementation with dual-provider support (SQL Server native BACKUP/RESTORE with master connection, SINGLE_USER WITH ROLLBACK IMMEDIATE, ClearAllPools; SQLite native backup & pool clearing for test harness) and SemaphoreSlim(1,1) concurrency guard
- JsonBackupSettingsStore implementation persisting to %LocalAppData%\SupermarketSystem\backup_settings.json
- DI registration in CoreServiceCollectionExtensions.cs
- Comprehensive unit tests in tests/Supermarket.Core.Tests/BackupServiceTests.cs
Build and run tests to verify. Write your final report to handoff.md and send a message when finished.

