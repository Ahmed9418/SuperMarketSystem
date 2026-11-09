# Milestone 1 Independent Review & Adversarial Stress-Test Report

**Reviewer**: `teamwork_reviewer_m1_2`  
**Roles**: Reviewer & Adversarial Critic  
**Milestone**: Milestone 1 (Core Backup & Settings Subsystem)  
**Target Work Product**: `Supermarket.Core` Backup Subsystem (Worker M1)  
**Verdict**: **REQUEST_CHANGES**  
**Date**: 2026-11-09T14:17:00Z  

---

## 1. Observation

### Observation 1: Test Failures on Corrupt File Restoration & Cancellation Tokens
Running the test suite via:
`dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-build -p:NuGetAudit=false --filter FullyQualifiedName~Backup`
yielded 4 test failures:
```
Failed!  - Failed: 4, Passed: 14, Skipped: 0, Total: 18, Duration: 2 s - Supermarket.Core.Tests.dll (net8.0)

[FAIL] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_InMemoryDb_MustReturnFailed
  Error Message: Adversarial probe: Restoring from a corrupted file containing random bytes MUST return Success = false.
  Stack Trace: at Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_InMemoryDb_MustReturnFailed() in AdversarialBackupTests.cs:line 80

[FAIL] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_PhysicalDb_MustNotCorruptOrDestroyDatabase
  Error Message: Restoring from corrupt file must return Success = false.
  Stack Trace: at Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_PhysicalDb_MustNotCorruptOrDestroyDatabase() in AdversarialBackupTests.cs:line 126

[FAIL] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_CreateBackupWithCancelledToken_ReturnsFailed
  Error Message: System.Threading.Tasks.TaskCanceledException : A task was canceled.
  Stack Trace: at Supermarket.Core.Services.BackupService.CreateBackupAsync(...) in BackupService.cs:line 58

[FAIL] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreWithCancelledToken_ReturnsFailed
  Error Message: System.Threading.Tasks.TaskCanceledException : A task was canceled.
  Stack Trace: at Supermarket.Core.Services.BackupService.RestoreDatabaseAsync(...) in BackupService.cs:line 109
```
Running the Admin E2E suite via:
`dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-build -p:NuGetAudit=false`
yielded identical failures in `BackupRecoveryE2ETests.cs`:
```
[FAIL] Supermarket.Admin.Tests.E2E.BackupRecoveryE2ETests.Tier2_Restore_CorruptZeroByteFileFailsGracefully (Line 294)
  Expected: False, Actual: True
[FAIL] Supermarket.Admin.Tests.E2E.BackupRecoveryE2ETests.Tier2_Restore_CorruptHeaderOrGarbageContentFailsGracefully (Line 311)
  Expected: False, Actual: True
[FAIL] Supermarket.Admin.Tests.E2E.BackupRecoveryE2ETests.Tier4_RealWorld_FailedRestoreFromCorruptFile_LeavesLiveDatabaseOperational (Line 819)
  Expected: False, Actual: True
```

### Observation 2: Flawed Corrupt File Handling and Data Loss Hazard
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
- Line 516: When restoring to a physical database, `File.Copy(backupFilePath, dataSource, overwrite: true);` is called unconditionally even if `ExecuteSqliteNativeRestoreAsync` failed (`restored == false`). A corrupted backup file will overwrite the live production database with corrupt bytes!
- Line 527: `if (!canConnect && !restored)` only returns `Failed` when BOTH `canConnect` is false AND `restored` is false. If the database is in-memory or connection succeeded before restore, `canConnect` is `true`, meaning `!canConnect` is `false`, so `(!canConnect && !restored)` evaluates to `false`. The method falls through to line 533 and returns `RestoreResult.Succeeded()`, falsely reporting success for corrupt files.

### Observation 3: Unhandled Cancellation Exception Escapes Gate Wait
In `src/Supermarket.Core/Services/BackupService.cs`, lines 58–61 and 109–112:
```csharp
await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
try
{
    using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
...
```
`await _gate.WaitAsync(cancellationToken)` is located outside the `try { ... } catch (OperationCanceledException) { return BackupResult.Failed(...); }` block. If `cancellationToken` is cancelled before entering the gate or while waiting for the semaphore, `TaskCanceledException` / `OperationCanceledException` escapes unhandled.

### Observation 4: Instance Semaphore on Transient Service Registration
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
`_gate` is an instance field. Because `IBackupService` is registered as `Transient`, when `SettingsViewModel` and `AutoBackupScheduler` each resolve `IBackupService`, they receive distinct instances with separate semaphores. A manual backup and an automated backup will run concurrently without mutual exclusion.

### Observation 5: SQL Server `SET SINGLE_USER` Outside `try` Block
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
Setting `SINGLE_USER` is executed outside the `try` block. If an exception or cancellation occurs after setting `SINGLE_USER` but before entering `try`, or if `cmdSingle` partially executes, the database is left permanently in `SINGLE_USER` mode without hitting the `finally` block.

---

## 2. Logic Chain

1. **Evidence of Acceptance Criteria Failure (Observation 1 & 2)**:
   - Acceptance Criteria state: *"Invalid backup paths, corrupt restore files, or aborted operations do not crash the application"* and *"Restoring from a valid backup successfully replaces/restores the database state"*.
   - In `RestoreSqliteDatabaseAsync`, corrupt restore files either falsely return `Success = true` (in-memory) or overwrite the live file with corrupt bytes (physical DB). Both behaviors directly violate acceptance criteria and corrupt user state.

2. **Evidence of Unhandled Exceptions (Observation 1 & 3)**:
   - ORIGINAL_REQUEST §R4 states: *"Wrap all file input/output and database operations in strict try-catch blocks"* and *"Any failure during backup or restore logs the error and displays a modal warning dialog describing the issue"*.
   - Because `await _gate.WaitAsync(cancellationToken)` is outside the `try` block, cancelled operations throw unhandled exceptions, crashing calling UI or task threads instead of returning `BackupResult.Failed` / `RestoreResult.Failed`.

3. **Evidence of Concurrency Isolation Breakdown (Observation 4)**:
   - Worker M1 claimed concurrency serialization via `SemaphoreSlim(1, 1)`.
   - However, since `BackupService` is registered as `Transient` and `_gate` is non-static, any two distinct callers (e.g., UI `SettingsViewModel` and background `AutoBackupScheduler`) acquire independent gates simultaneously. The concurrency guard only serializes calls on the exact same service instance.

4. **Evidence of Lock Reset Hazard (Observation 5)**:
   - PROJECT.md R1 Flaw 2 emphasizes safe recovery from connection termination. Executing `SINGLE_USER` outside the `try` block leaves the database inaccessible to all supermarket clients if cancellation or failure occurs between lines 336 and 338.

---

## 3. Findings & Required Remediations

### Finding 1: [Critical] Corrupt Restore Reports False Success & Destroys Physical Database
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:506-534`
- **Issue**:
  1. `if (!canConnect && !restored)` returns `RestoreResult.Succeeded()` when `restored` is `false`.
  2. `File.Copy(backupFilePath, dataSource, overwrite: true)` is executed when `restored == false`, overwriting valid databases with corrupt files.
- **Required Fix**:
  ```csharp
  if (!restored)
  {
      _logger.LogError("SQLite database restore failed from file {Path}", backupFilePath);
      return RestoreResult.Failed("Database restore failed: The specified backup file is corrupt or invalid.");
  }

  // Only copy to physical file if native restore succeeded AND target file exists
  var dataSource = dbConn.DataSource;
  if (!string.IsNullOrWhiteSpace(dataSource) &&
      !string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase) &&
      File.Exists(dataSource))
  {
      // Already restored into dbConn via native BackupDatabase; no blind File.Copy needed,
      // or only copy after verifying backup file integrity.
  }
  ```

### Finding 2: [Critical] Unhandled `OperationCanceledException` Escapes Gate Wait
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:58-60, 109-111`
- **Issue**: Calling `_gate.WaitAsync(cancellationToken)` outside `try` causes `TaskCanceledException` to escape unhandled.
- **Required Fix**:
  Wrap gate wait with boolean tracking:
  ```csharp
  bool gateAcquired = false;
  try
  {
      await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
      gateAcquired = true;
      // ... execute operation ...
  }
  catch (OperationCanceledException)
  {
      _logger.LogWarning("Operation was cancelled.");
      return BackupResult.Failed("Backup operation was cancelled.");
  }
  catch (Exception ex)
  {
      _logger.LogError(ex, "Operation failed: {Message}", ex.Message);
      return BackupResult.Failed(ex.Message);
  }
  finally
  {
      if (gateAcquired)
      {
          _gate.Release();
      }
  }
  ```
  (Apply identical pattern to `RestoreDatabaseAsync`).

### Finding 3: [Major] Concurrency Guard `_gate` Fails Across Injected Instances
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:24` and `CoreServiceCollectionExtensions.cs:112`
- **Issue**: `_gate` is an instance field while `BackupService` is registered as `Transient`.
- **Required Fix**:
  Change `_gate` to process-wide static:
  ```csharp
  private static readonly SemaphoreSlim _gate = new(1, 1);
  ```
  And/or register `IBackupService` as `Singleton` in `CoreServiceCollectionExtensions.cs` (its dependencies `IDbContextFactory`, `IConfiguration`, and `ILogger` are all singletons).

### Finding 4: [Major] SQL Server `SET SINGLE_USER` Placed Outside `try` Block
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:331-355`
- **Issue**: If `cmdSingle` executes but restore does not start, the database remains trapped in `SINGLE_USER`.
- **Required Fix**:
  Move `cmdSingle` inside the `try` block before `cmdRestore`, so that `finally` guarantees `SET MULTI_USER` execution:
  ```csharp
  try
  {
      using (var cmdSingle = masterConn.CreateCommand())
      {
          cmdSingle.CommandText = $"ALTER DATABASE [{safeDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
          cmdSingle.CommandTimeout = 120;
          await cmdSingle.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
      }

      using (var cmdRestore = masterConn.CreateCommand())
      {
          cmdRestore.CommandText = $"RESTORE DATABASE [{safeDbName}] FROM DISK = @path WITH REPLACE;";
          cmdRestore.CommandTimeout = 600;
          cmdRestore.Parameters.Add(new SqlParameter("@path", backupFilePath));
          await cmdRestore.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
      }
  }
  finally
  {
      // Step 4: Always revert database to MULTI_USER mode
      ...
  }
  ```

### Finding 5: [Minor] Temporary Probe File in `CanWriteToPathAsync` Cleanup Safety
- **Location**: `src/Supermarket.Core/Services/BackupService.cs:177-183`
- **Issue**: Probe file deletion is not inside a `finally` block; unexpected exceptions could leave `.probe_*.tmp` files behind.
- **Required Fix**: Use `try { ... } finally { if (File.Exists(probeFile)) File.Delete(probeFile); }`.

---

## 4. Caveats

- Unit tests in `Supermarket.Core.Tests` execute against SQLite (in-memory and file-based). The SQL Server branches were reviewed via static code analysis against Microsoft SQL Server T-SQL and ADO.NET specifications.
- `JsonBackupSettingsStore` was thoroughly reviewed and tested: its `SemaphoreSlim`, atomic temp-file replace, and corrupt JSON fallback are sound and passed all tests.

---

## 5. Conclusion

**Verdict: REQUEST_CHANGES**

Milestone 1 cannot be approved in its current state due to 2 Critical and 2 Major defects:
1. Restoring a corrupt file reports success instead of failing, and destroys physical databases.
2. Unhandled cancellation exceptions crash callers when cancelled at the semaphore gate.
3. Concurrency guard does not serialize operations across independent service injections.
4. `SINGLE_USER` mode is set outside the protected `try` block.

Worker M1 must address these findings in `src/Supermarket.Core/Services/BackupService.cs` and re-run all unit and E2E tests before approval.

---

## 6. Verification Method

To verify the fixes, execute:
```powershell
# 1. Build Supermarket.Core
dotnet build src/Supermarket.Core/Supermarket.Core.csproj --no-restore -p:NuGetAudit=false

# 2. Run all Core Backup tests including Adversarial tests
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-build -p:NuGetAudit=false --filter FullyQualifiedName~Backup

# 3. Run Admin E2E Backup tests
dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-build -p:NuGetAudit=false --filter FullyQualifiedName~Backup
```
Expected result: 100% tests passing (0 failures).
