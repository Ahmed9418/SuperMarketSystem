# Milestone 1 Empirical Challenge & Stress-Test Report

**Agent**: `teamwork_challenger_m1_1`  
**Milestone**: Milestone 1 (Core Backup & Settings Subsystem)  
**Status**: Hard Handoff — **VERDICT: REJECT**  
**Date**: 2026-11-09T14:28:00Z  

---

## 1. Observation

### 1.1 Empirical Test Execution Commands & Verbatim Failures

1. **`Supermarket.Core.Tests.AdversarialBackupTests` Failures**:
   - Command:
     ```powershell
     dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~AdversarialBackupTests
     ```
   - Verbatim Failures:
     ```
     Failed Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_InMemoryDb_EmpiricalResult
     Error Message: BUG CONFIRMED: BackupService reported Success=true when restoring a corrupt file containing random bytes! ErrorMessage: ''

     Failed Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_PhysicalDb_EmpiricalResult
     Error Message: BUG CONFIRMED: BackupService reported Success=true on physical DB when restoring a corrupt file! ErrorMessage: ''

     Failed Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreZeroByteFile_MustReturnFailed
     Error Message: BUG CONFIRMED: Restoring from a 0-byte file reported Success=true! ErrorMessage: ''

     Failed Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_PreCancelledToken_CreateBackup_ThrowsInsteadOfReturningFailed
     Error Message: Assert.Null() Failure: Value is not null
     Actual: System.Threading.Tasks.TaskCanceledException: A task was canceled.
        at Supermarket.Core.Services.BackupService.CreateBackupAsync(String destinationFilePath, CancellationToken cancellationToken) in BackupService.cs:line 58

     Failed Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_PreCancelledToken_Restore_ThrowsInsteadOfReturningFailed
     Error Message: Assert.Null() Failure: Value is not null
     Actual: System.Threading.Tasks.TaskCanceledException: A task was canceled.
        at Supermarket.Core.Services.BackupService.RestoreDatabaseAsync(String backupFilePath, CancellationToken cancellationToken) in BackupService.cs:line 109
     ```

2. **`Supermarket.Admin.Tests.E2E.BackupRecoveryE2ETests` Failures**:
   - Command:
     ```powershell
     dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~E2E
     ```
   - Verbatim Failures:
     ```
     Failed Supermarket.Admin.Tests.E2E.BackupRecoveryE2ETests.Tier2_Restore_CorruptZeroByteFileFailsGracefully
     Error Message: Assert.False() Failure (Expected: False, Actual: True) at line 294

     Failed Supermarket.Admin.Tests.E2E.BackupRecoveryE2ETests.Tier2_Restore_CorruptHeaderOrGarbageContentFailsGracefully
     Error Message: Assert.False() Failure (Expected: False, Actual: True) at line 311

     Failed Supermarket.Admin.Tests.E2E.BackupRecoveryE2ETests.Tier4_RealWorld_FailedRestoreFromCorruptFile_LeavesLiveDatabaseOperational
     Error Message: Assert.False() Failure (Expected: False, Actual: True) at line 817
     ```

3. **`Supermarket.Core.Tests.Challenger1StressTests` Execution**:
   - Path: `tests/Supermarket.Core.Tests/Challenger1StressTests.cs`
   - Command:
     ```powershell
     dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Challenger1StressTests
     ```
   - Results: 10 Passed, 1 Failed.
   - Verbatim Failure:
     ```
     Failed Supermarket.Core.Tests.Challenger1StressTests.CanWriteToPathAsync_VariousValidAndInvalidPaths_EvaluatedCorrectly
     Error Message: Assert.False() Failure (Expected: False, Actual: True)
     Stack Trace: at Challenger1StressTests.cs:line 307
     ```

### 1.2 Code Inspection Observations

1. **`src/Supermarket.Core/Services/BackupService.cs` Lines 526–534**:
   ```csharp
   bool canConnect = await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
   if (!canConnect && !restored)
   {
       return RestoreResult.Failed("Could not verify database connectivity after restore.");
   }

   _logger.LogInformation("SQLite database restored successfully from {Path}", backupFilePath);
   return RestoreResult.Succeeded();
   ```

2. **`src/Supermarket.Core/Services/BackupService.cs` Lines 508–522**:
   ```csharp
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
   ```

3. **`src/Supermarket.Core/Services/BackupService.cs` Lines 58 & 109**:
   ```csharp
   // Line 58 in CreateBackupAsync:
   await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
   try
   { ... }
   catch (OperationCanceledException) { ... }

   // Line 109 in RestoreDatabaseAsync:
   await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
   try
   { ... }
   catch (OperationCanceledException) { ... }
   ```

4. **`src/Supermarket.Core/Services/BackupService.cs` Lines 154–184**:
   `CanWriteToPathAsync` extracts `dir = Path.GetDirectoryName(destinationPath);` and creates a `.probe_{Guid}.tmp` file in `dir`. It never checks whether `destinationPath` contains illegal file characters (`Path.GetInvalidFileNameChars()`).

---

## 2. Logic Chain

1. **False-Positive Restore Success on Corrupt/Zero-Byte Files (Critical Data Integrity Flaw)**:
   - *Observation 1.1 (#1, #2) & Observation 1.2 (#1)*:
     When restoring a corrupted file (random bytes, truncated header, or 0 bytes), `ExecuteSqliteNativeRestoreAsync` returns `false` (`restored == false`).
     However, the subsequent check evaluates `if (!canConnect && !restored)`.
     Because the current database connection is alive and open, `canConnect` is `true`, making `!canConnect` `false`.
     Consequently, `(!canConnect && !restored)` evaluates to `false`. The failure block is skipped, and `RestoreResult.Succeeded()` is returned.
     The user/caller is falsely informed that the database was restored successfully when in reality no restoration occurred.

2. **Physical Database File Overwrite & Data Destruction on Corrupt Restore (Critical Disaster Risk)**:
   - *Observation 1.1 (#1) & Observation 1.2 (#2)*:
     For a file-based SQLite database, lines 508–522 unconditionally execute `File.Copy(backupFilePath, dataSource, overwrite: true)` before verifying whether `backupFilePath` contains a valid, readable SQLite database.
     When a corrupt or truncated file is provided, `File.Copy` overwrites the active production database file (`dataSource`) with the corrupt bytes, obliterating all store data and leaving the physical database unconnectable.

3. **Unhandled `TaskCanceledException` on Cancellation (Safety & Contract Violation)**:
   - *Observation 1.1 (#1) & Observation 1.2 (#3)*:
     `ORIGINAL_REQUEST.md` (R4) mandates: "Wrap all file input/output and database operations in strict try-catch blocks. Aborted operations do not crash the application."
     In both `CreateBackupAsync` and `RestoreDatabaseAsync`, `_gate.WaitAsync(cancellationToken)` is located immediately *before* the `try` block that handles `OperationCanceledException`.
     If a caller provides a pre-cancelled token or cancels while waiting for the semaphore lock, `_gate.WaitAsync` throws `TaskCanceledException` directly out of the method, causing unexpected unhandled task exceptions instead of returning `BackupResult.Failed("Backup operation was cancelled.")` or `RestoreResult.Failed("Restore operation was cancelled.")`.

4. **False Positive in `CanWriteToPathAsync` for Invalid Filenames**:
   - *Observation 1.1 (#3) & Observation 1.2 (#4)*:
     `CanWriteToPathAsync` only probes the directory. When given a destination path with illegal filename characters such as `Path.Combine(tempDir, "invalid|file<name>?.bak")`, `CanWriteToPathAsync` returns `true`. Attempting to back up to this path subsequently fails.

5. **Subsystems Verified as Robust**:
   - *Observation 1.1 (#3)*:
     - 10 simultaneous backup operations executed concurrently without collisions, deadlocks, or corruption (`Concurrency_10SimultaneousBackups_AllSucceedWithDistinctValidFiles`: PASSED).
     - Concurrent backup and restore calls were safely serialized by `SemaphoreSlim(1,1)` without exceptions (`Concurrency_SimultaneousBackupAndRestore_SerializedWithoutExceptions`: PASSED).
     - SQLite in-memory and physical file-based backups produce standalone, directly queryable SQLite databases (`StandaloneRestorability_InMemoryDb_ProducesDirectlyQueryableSqliteFile` & `StandaloneRestorability_FileBasedDb_ProducesDirectlyQueryableSqliteFile`: PASSED).
     - `JsonBackupSettingsStore` creates missing directory trees, gracefully handles 0-byte, whitespace, and malformed JSON, and maintains data consistency across 10 concurrent writers and 10 concurrent readers (`JsonBackupSettingsStore_*`: PASSED).

---

## 3. Caveats

- **SQL Server Physical Verification**: In this test environment, tests ran against SQLite (both in-memory and file-based). Direct T-SQL execution against an active Microsoft SQL Server instance was evaluated by static inspection and T-SQL grammar analysis; the SQL Server restore logic has the proper `SINGLE_USER WITH ROLLBACK IMMEDIATE` and `MULTI_USER` reset semantics.
- **Pre-existing test failure**: `ReceiptAndShiftClosingTests.GetShifts_ReturnsHistoryWithinTheDateRange` failed due to date boundary filtering in pre-existing tests unrelated to Milestone 1.

---

## 4. Conclusion

**Verdict: REJECT**

Milestone 1 **cannot be approved** in its current state due to two blocking critical flaws and one validation defect:

1. **[CRITICAL] Data Loss & False Success on Restore**:
   - `RestoreSqliteDatabaseAsync` reports `Success = true` when given corrupt or 0-byte backup files.
   - It overwrites and destroys the active physical SQLite database with corrupt bytes before validating file integrity.
2. **[HIGH] Unhandled Cancellation Exceptions**:
   - Calling `CreateBackupAsync` or `RestoreDatabaseAsync` with a cancelled token throws an unhandled `TaskCanceledException` because `_gate.WaitAsync` is outside the `try` block.
3. **[MEDIUM] Incomplete Path Validation**:
   - `CanWriteToPathAsync` returns `true` for file paths containing illegal filename characters (`|`, `<`, `>`, `?`, `*`).

### Required Fixes for `teamwork_worker_m1`:
1. In `BackupService.cs`, wrap `await _gate.WaitAsync(cancellationToken)` inside the `try` block (or catch `OperationCanceledException` around it).
2. In `RestoreSqliteDatabaseAsync`:
   - Validate that `backupFilePath` is a valid, readable SQLite database *before* attempting restore or copying.
   - If `!restored`, do NOT overwrite the physical database file; immediately return `RestoreResult.Failed(...)`.
   - Ensure `if (!restored)` returns `RestoreResult.Failed("Backup file is corrupt or not a valid database.")`.
3. In `CanWriteToPathAsync`, validate that `Path.GetFileName(destinationPath)` does not contain any characters from `Path.GetInvalidFileNameChars()`.

---

## 5. Verification Method

To independently reproduce the failures:

```powershell
# 1. Run Challenger 1 Stress Suite (demonstrating invalid filename false-positive):
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Challenger1StressTests

# 2. Run Adversarial Suite (demonstrating false success on corrupt file, live DB overwrite, and unhandled cancellation):
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~AdversarialBackupTests

# 3. Run E2E Test Suite (demonstrating corrupt restore failures in Admin suite):
dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Tier2_Restore_Corrupt
```

Files to inspect:
- `src/Supermarket.Core/Services/BackupService.cs` (lines 58, 109, 154–190, 506–534)
- `tests/Supermarket.Core.Tests/Challenger1StressTests.cs`
- `tests/Supermarket.Core.Tests/AdversarialBackupTests.cs`
- `tests/Supermarket.Admin.Tests/E2E/BackupRecoveryE2ETests.cs`
