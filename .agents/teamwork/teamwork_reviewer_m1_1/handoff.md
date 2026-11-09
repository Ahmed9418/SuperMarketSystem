# Milestone 1 Independent Review & Adversarial Stress-Test Report

**Reviewer**: `teamwork_reviewer_m1_1`  
**Roles**: Reviewer & Adversarial Critic  
**Milestone**: Milestone 1 (Core Backup & Settings Subsystem in `Supermarket.Core`)  
**Target Work Product**: Worker M1 (`teamwork_worker_m1`)  
**Verdict**: **REQUEST_CHANGES**  
**Date**: 2026-11-09T14:23:00Z  

---

## 1. Observation

### Observation 1: Test Suite Results Across Independent Runs
- **Compilation**:
  - `dotnet build src/Supermarket.Core/Supermarket.Core.csproj --no-restore -p:NuGetAudit=false`
    Output: `Build succeeded. 0 Warning(s), 0 Error(s)`.
  - `dotnet build tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false`
    Output: `Build succeeded. 0 Warning(s), 0 Error(s)`.
- **Test Executions**:
  - Running Worker M1's original 12 tests (`--filter FullyQualifiedName~BackupServiceTests`):
    Output: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 2 s`.
  - Running full Backup suite including adversarial stress-tests (`AdversarialBackupTests.cs` and `BackupRecoveryE2ETests.cs`):
    The adversarial tests failed with 4 failures in `Supermarket.Core.Tests`:
    ```
    [FAIL] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_InMemoryDb_MustReturnFailed
      Error Message: Adversarial probe: Restoring from a corrupted file containing random bytes MUST return Success = false.
      Location: AdversarialBackupTests.cs:line 80
    [FAIL] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_PhysicalDb_MustNotCorruptOrDestroyDatabase
      Error Message: Restoring from corrupt file must return Success = false.
      Location: AdversarialBackupTests.cs:line 126
    [FAIL] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_CreateBackupWithCancelledToken_ReturnsFailed
      Error Message: System.Threading.Tasks.TaskCanceledException : A task was canceled.
      Location: BackupService.cs:line 58
    [FAIL] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreWithCancelledToken_ReturnsFailed
      Error Message: System.Threading.Tasks.TaskCanceledException : A task was canceled.
      Location: BackupService.cs:line 109
    ```

### Observation 2: SQLite Corrupt File Handling & Database Destruction
In `src/Supermarket.Core/Services/BackupService.cs`, lines 506–534:
```csharp
bool restored = await ExecuteSqliteNativeRestoreAsync(dbConn, backupFilePath, cancellationToken).ConfigureAwait(false);

var dataSource = dbConn.DataSource;
if (!string.IsNullOrWhiteSpace(dataSource) &&
    !string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase) &&
    File.Exists(dataSource))
{
    try
    {
        ClearSqlitePools();
        File.Copy(backupFilePath, dataSource, overwrite: true);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to overwrite physical SQLite database file at {Path}", dataSource);
    }
}

ClearSqlitePools();

bool canConnect = await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
if (!canConnect && !restored)
{
    return RestoreResult.Failed("Could not verify database connectivity after restore.");
}

_logger.LogInformation("SQLite database restored successfully from {Path}", backupFilePath);
return RestoreResult.Succeeded();
```
- Line 516 calls `File.Copy(backupFilePath, dataSource, overwrite: true);` unconditionally when `File.Exists(dataSource)` is true, completely ignoring whether `restored` is `false`. A corrupt or garbage file is copied directly over the live physical database, destroying valid database state.
- Line 527 evaluates `if (!canConnect && !restored)`. For in-memory databases (`:memory:`), `canConnect` is `true`, making `!canConnect` `false`. Consequently, `(!canConnect && !restored)` evaluates to `false`, falling through to line 533 and returning `RestoreResult.Succeeded()` when restoring a corrupt file.

### Observation 3: Cancellation Exceptions Escape Concurrency Gate Wait
In `src/Supermarket.Core/Services/BackupService.cs`, lines 58–61 and 109–112:
```csharp
// In CreateBackupAsync:
await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
try
{
    using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
...

// In RestoreDatabaseAsync:
await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
try
{
    using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
...
```
`_gate.WaitAsync(cancellationToken)` is placed outside the `try` block. If `cancellationToken` is already cancelled or triggered while waiting in queue, an unhandled `OperationCanceledException` / `TaskCanceledException` escapes the method, crashing callers instead of returning `BackupResult.Failed` or `RestoreResult.Failed`.

### Observation 4: Instance Semaphore on Transient DI Registration
In `src/Supermarket.Core/Services/BackupService.cs`, line 24:
```csharp
public sealed class BackupService : IBackupService
{
    ...
    private readonly SemaphoreSlim _gate = new(1, 1);
```
In `src/Supermarket.Core/DependencyInjection/CoreServiceCollectionExtensions.cs`, line 112:
```csharp
services.AddTransient<IBackupService, BackupService>();
```
`_gate` is an instance field. Because `IBackupService` is registered as `Transient`, separate consumers (e.g. `SettingsViewModel` for manual user backups and `AutoBackupScheduler` for periodic backups) receive distinct `BackupService` instances each with their own isolated `SemaphoreSlim`. Concurrent executions are NOT serialized across instances.

### Observation 5: SQL Server `SINGLE_USER` Mode Set Outside `try` Block
In `src/Supermarket.Core/Services/BackupService.cs`, lines 331–347:
```csharp
using (var cmdSingle = masterConn.CreateCommand())
{
    cmdSingle.CommandText = $"ALTER DATABASE [{safeDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
    cmdSingle.CommandTimeout = 120;
    await cmdSingle.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}

try
{
    // Step 3: Execute RESTORE DATABASE
    using var cmdRestore = masterConn.CreateCommand();
    cmdRestore.CommandText = $"RESTORE DATABASE [{safeDbName}] FROM DISK = @path WITH REPLACE;";
    ...
}
finally
{
    ...
    cmdMulti.CommandText = $"ALTER DATABASE [{safeDbName}] SET MULTI_USER;";
    ...
}
```
Setting `SINGLE_USER` is performed before entering the `try` block. If an exception or cancellation occurs after executing `cmdSingle` but before entering `try`, or if `cmdRestore` construction throws, the `finally` block is never executed. The database remains locked in `SINGLE_USER` mode, blocking all client connections and taking the system down.

---

## 2. Logic Chain

1. **Acceptance Criteria Violation (Observation 1 & 2)**:
   - Acceptance Criteria state: *"Invalid backup paths, corrupt restore files, or aborted operations do not crash the application"* and *"Restoring from a valid backup successfully replaces/restores the database state"*.
   - In `RestoreSqliteDatabaseAsync`, corrupt files falsely report `Success = true` on in-memory databases, and overwrite the physical database file with garbage bytes on disk-backed databases. This is a severe safety and data loss flaw.
2. **Unhandled Exception Violation (Observation 1 & 3)**:
   - Requirement R4 states: *"Wrap all file input/output and database operations in strict try-catch blocks"* and *"Any failure during backup or restore logs the error and displays a modal warning dialog describing the issue"*.
   - Placing `_gate.WaitAsync(cancellationToken)` outside the `try` block allows `TaskCanceledException` to escape unhandled, crashing caller threads instead of gracefully returning failure results.
3. **Concurrency Serialization Defect (Observation 4)**:
   - FLAW_ANALYSIS §Flaw 4 and PROJECT §Architecture specify that all backup and restore operations must be serialized via `SemaphoreSlim(1, 1)`.
   - Because `_gate` is an instance field on a `Transient` service, two injected callers do not share the lock. Scheduled auto-backups and manual user operations can collide.
4. **Lock Recovery Defect (Observation 5)**:
   - FLAW_ANALYSIS §Flaw 2 mandates safe database restoration. Setting `SINGLE_USER` outside the `try` block leaves the production database permanently locked if cancellation or failure occurs immediately after the command executes.

---

## 3. Review Findings & Required Remediations

### Finding 1: [Critical] Corrupt File Restoration Reports False Success & Destroys Live Database
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:506-534`
- **Why**: Blind `File.Copy` overwrites live physical database when restore fails, and `if (!canConnect && !restored)` allows false success returns.
- **Suggestion**:
  - Immediately check `if (!restored) return RestoreResult.Failed("Database restore failed: The specified backup file is corrupt or invalid.");`.
  - Never execute `File.Copy` when `restored == false`.

### Finding 2: [Critical] Unhandled `OperationCanceledException` Escapes Concurrency Gate
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:58-60, 109-111`
- **Why**: `await _gate.WaitAsync(cancellationToken)` is outside `try`.
- **Suggestion**:
  - Enclose `_gate.WaitAsync` within `try` using boolean gate tracking:
    ```csharp
    bool gateAcquired = false;
    try
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        gateAcquired = true;
        ...
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Operation was cancelled.");
        return BackupResult.Failed("Backup operation was cancelled.");
    }
    finally
    {
        if (gateAcquired) _gate.Release();
    }
    ```
  - Apply the identical pattern to `RestoreDatabaseAsync`.

### Finding 3: [Major] Concurrency Gate Fails Across Injected Instances
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:24` and `CoreServiceCollectionExtensions.cs:112`
- **Why**: `_gate` is an instance field on a `Transient` service.
- **Suggestion**:
  - Make `_gate` a process-wide static field: `private static readonly SemaphoreSlim _gate = new(1, 1);`
  - And/or register `IBackupService` as `Singleton` in DI (its dependencies are thread-safe singletons).

### Finding 4: [Major] SQL Server `SET SINGLE_USER` Placed Outside `try` Block
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:331-355`
- **Why**: Setting `SINGLE_USER` before `try` risks leaving the database locked in `SINGLE_USER` mode if any error or cancellation occurs before the restore command starts.
- **Suggestion**:
  - Move `cmdSingle.ExecuteNonQueryAsync` inside the `try` block so that `finally` is guaranteed to execute `SET MULTI_USER;`.

### Finding 5: [Minor] Probe File Cleanup Safety in `CanWriteToPathAsync`
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:177-183`
- **Why**: `File.Delete(probeFile)` is not in a `finally` block; unexpected exceptions could leave temporary probe files.
- **Suggestion**: Wrap probe file creation and deletion in `try ... finally`.

---

## 4. Adversarial Challenge & Stress-Test Summary

**Overall Risk Assessment**: **HIGH**

| Challenge ID | Scenario Tested | Blast Radius | Observed Behavior | Status |
|---|---|---|---|---|
| ADV-1 | Restore 2KB corrupt file with random bytes on physical SQLite DB | Production database file overwritten with corrupt bytes | Live database destroyed | **FAIL** |
| ADV-2 | Restore corrupt file on in-memory SQLite DB | Database reports false success | Returns `RestoreResult.Succeeded()` | **FAIL** |
| ADV-3 | Backup/Restore invocation with pre-cancelled token | Caller thread crashed with unhandled exception | Throws `TaskCanceledException` | **FAIL** |
| ADV-4 | Simultaneous backup via two distinct DI-injected instances | Mutual exclusion bypassed; concurrent DB writes | Instance semaphores isolated | **FAIL** |
| ADV-5 | Corrupt JSON settings loaded via `JsonBackupSettingsStore` | Settings corruption causes crash | Gracefully falls back to defaults | **PASS** |
| ADV-6 | Concurrent reads and writes to `JsonBackupSettingsStore` | File lock collision / corrupt file | Serialized safely via store semaphore | **PASS** |

---

## 5. Verified Claims vs Integrity Assessment

- **Integrity Assessment**: **NO INTEGRITY VIOLATION DETECTED**
  - No hardcoded test responses or facade stubs were found in `Supermarket.Core`.
  - The implementation uses genuine ADO.NET and EF Core commands.
  - However, Worker M1's test suite was selective and omitted critical error conditions (corrupt files, cancelled tokens, multi-instance concurrency), obscuring the severe defects identified above.
- **Verified Items**:
  - `JsonBackupSettingsStore`: Verified atomic temp-file replace, `%LocalAppData%` path resolution, and corrupt JSON tolerance. (PASS)
  - `CreateBackupAsync` valid path execution: Verified physical file creation and non-zero size. (PASS)
  - `RestoreDatabaseAsync` valid round-trip: Verified entity state restoration. (PASS)

---

## 6. Caveats

- Tests executed in the local environment use SQLite fixtures (`SqliteTestDatabase`, `TestContextFactory`). SQL Server specific routines (`CreateSqlServerBackupAsync`, `RestoreSqlServerDatabaseAsync`) were verified via static analysis against Microsoft SQL Server T-SQL and ADO.NET specifications.
- `Supermarket.Admin` and `Supermarket.Admin.Tests` currently have ongoing modifications by Milestone 2 worker and test writer.

---

## 7. Conclusion

**Verdict: REQUEST_CHANGES**

Milestone 1 CANNOT be approved in its current state. Worker M1 must address Findings 1 through 4 in `src/Supermarket.Core/Services/BackupService.cs` and `CoreServiceCollectionExtensions.cs` to ensure that:
1. Corrupted backup files are rejected with `RestoreResult.Failed` and do not corrupt or overwrite live databases.
2. Cancelled operations cleanly return failure results without throwing unhandled exceptions.
3. Concurrency serialization is enforced across all injected `BackupService` instances.
4. Database single-user mode is safely unwound in `finally` blocks.

---

## 8. Verification Method

To independently verify the remediated implementation, execute:
```powershell
# 1. Compile Supermarket.Core
dotnet build src/Supermarket.Core/Supermarket.Core.csproj --no-restore -p:NuGetAudit=false

# 2. Compile Supermarket.Core.Tests
dotnet build tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false

# 3. Run all Backup tests (including AdversarialBackupTests)
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-build -p:NuGetAudit=false --filter FullyQualifiedName~Backup
```
Expected result: 100% tests passing (18 of 18 passed, 0 failed).
