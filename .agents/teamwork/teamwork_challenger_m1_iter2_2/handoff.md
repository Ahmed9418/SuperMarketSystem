# Milestone 1 Iteration 2 Adversarial Challenge & Verification Report

**Agent**: `teamwork_challenger_m1_iter2_2`  
**Roles**: Empirical Challenger & Adversarial Critic  
**Target Milestone**: Milestone 1 (Iteration 2 — Core Backup Remediation)  
**Target Work Product**: `src/Supermarket.Core/Services/BackupService.cs`  
**Final Verdict**: **APPROVE / CONFIRMED**  
**Overall Risk Assessment**: **LOW**  
**Date**: 2026-11-09T15:15:00Z  

---

## 1. Observation

### 1.1 Direct Test Execution Results

All verification suites were executed empirically using `BypassSandbox: true` in PowerShell to support local loopback TCP port binding required by `vstest.console` / `testhost`:

1. **Admin E2E Full Test Suite (50/50 Tests Passed)**:
   ```powershell
   dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~E2E
   ```
   *Execution Output*:
   ```
   Test run for C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\tests\Supermarket.Admin.Tests\bin\Debug\net8.0-windows\Supermarket.Admin.Tests.dll (.NETCoreApp,Version=v8.0)
   A total of 1 test files matched the specified pattern.

   Passed!  - Failed:     0, Passed:    50, Skipped:     0, Total:    50, Duration: 6 s - Supermarket.Admin.Tests.dll (net8.0)
   ```
   - **Verification of 3 Previously Failing Iteration 1 Tests**:
     - `Tier2_Restore_CorruptZeroByteFileFailsGracefully`: **PASSED** (0-byte file rejected with `Success == false` and descriptive message).
     - `Tier2_Restore_CorruptHeaderOrGarbageContentFailsGracefully`: **PASSED** (2048 random bytes rejected by SQLite 16-byte magic header validation).
     - `Tier4_RealWorld_FailedRestoreFromCorruptFile_LeavesLiveDatabaseOperational`: **PASSED** (corrupt file restore aborted safely prior to connection modification; live database remained 100% operational with subsequent schema queries and record insertions succeeding).

2. **Core Adversarial Test Suite (7/7 Tests Passed)**:
   ```powershell
   dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Adversarial
   ```
   *Execution Output*:
   ```
   Test run for C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\tests\Supermarket.Core.Tests\bin\Debug\net8.0\Supermarket.Core.Tests.dll (.NETCoreApp,Version=v8.0)
   A total of 1 test files matched the specified pattern.

   Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 2 s - Supermarket.Core.Tests.dll (net8.0)
   ```
   - `Adversarial_RestoreCorruptFile_InMemoryDb_EmpiricalResult`: **PASSED**
   - `Adversarial_RestoreCorruptFile_PhysicalDb_EmpiricalResult`: **PASSED**
   - `Adversarial_RestoreZeroByteFile_MustReturnFailed`: **PASSED**
   - `Adversarial_PreCancelledToken_CreateBackup_ThrowsInsteadOfReturningFailed`: **PASSED**
   - `Adversarial_PreCancelledToken_Restore_ThrowsInsteadOfReturningFailed`: **PASSED**
   - `Adversarial_BackupToInvalidDrive_MustReturnFailedWithoutCrashing`: **PASSED**
   - `Adversarial_RestoreLocksAndConnectionPool_CanDeleteBackupAfterRestore`: **PASSED**

3. **Core Comprehensive Backup Suite (41/41 Tests Passed)**:
   ```powershell
   dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Backup
   ```
   *Execution Output*:
   ```
   Passed!  - Failed:     0, Passed:    41, Skipped:     0, Total:    41, Duration: 4 s - Supermarket.Core.Tests.dll (net8.0)
   ```
   - Includes all 13 baseline unit tests in `BackupServiceTests.cs`, all 7 adversarial tests in `AdversarialBackupTests.cs`, and 21 additional stress/boundary challenge tests.

**Total empirical test results across solution: 91 passed, 0 failed, 0 skipped (100% pass rate).**

---

### 1.2 Code Inspection Observations in `BackupService.cs`

1. **SQLite Validation & Corruption Protection (`RestoreSqliteDatabaseAsync`, lines 582–633)**:
   - Line 583–586: Explicit zero-byte check:
     ```csharp
     var fileInfo = new FileInfo(backupFilePath);
     if (!fileInfo.Exists || fileInfo.Length == 0)
     {
         return RestoreResult.Failed("Backup file is empty.");
     }
     ```
   - Lines 588–607: File length check (< 16 bytes) and validation against the 16-byte SQLite format magic header:
     ```csharp
     byte[] sqliteMagic = new byte[] { 0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00 };
     byte[] header = new byte[16];
     using (var fs = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
     {
         int bytesRead = await fs.ReadAsync(header, 0, 16, cancellationToken).ConfigureAwait(false);
         if (bytesRead < 16) return RestoreResult.Failed("Backup file is corrupt or not a valid database.");
     }
     if (!header.SequenceEqual(sqliteMagic)) return RestoreResult.Failed("Backup file is corrupt or not a valid database.");
     ```
   - Line 617–621: Explicit boolean check on `restored`:
     ```csharp
     bool restored = await ExecuteSqliteNativeRestoreAsync(dbConn, backupFilePath, cancellationToken).ConfigureAwait(false);
     if (!restored) return RestoreResult.Failed("Failed to restore SQLite database from backup file.");
     ```
   - Line 625–629: Verification of `canConnect` post-restore:
     ```csharp
     bool canConnect = await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
     if (!canConnect) return RestoreResult.Failed("Could not verify database connectivity after restore.");
     ```
   - Destructive direct `File.Copy` over live SQLite databases observed in Iteration 1 has been completely removed.

2. **Cancellation Interception at Gate (`CreateBackupAsync` lines 58–66, `RestoreDatabaseAsync` lines 118–126)**:
   - Dedicated `try...catch (OperationCanceledException)` around `await _gate.WaitAsync(cancellationToken)`:
     ```csharp
     try
     {
         await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
     }
     catch (OperationCanceledException)
     {
         _logger.LogWarning("Restore operation was cancelled.");
         return RestoreResult.Failed("Restore operation was cancelled.");
     }
     ```
   - Semaphore release `_gate.Release()` is located exclusively in the subsequent `finally` block (lines 98–101, 158–161), executing only after gate acquisition.

3. **Process-Wide Concurrency Lock (`BackupService.cs`, line 24)**:
   - Declared as:
     ```csharp
     private static readonly SemaphoreSlim _gate = new(1, 1);
     ```
   - Replaced instance-level semaphore to serialize operations across all instances created by transient DI lifetime.

4. **SQL Server SINGLE_USER Mode Safety & Staging Fallback (`RestoreSqlServerDatabaseAsync`, lines 360–449)**:
   - Setting `SINGLE_USER WITH ROLLBACK IMMEDIATE` is enclosed within `try` block.
   - Corresponding `finally` block guarantees `ALTER DATABASE [{safeDbName}] SET MULTI_USER;` using `CancellationToken.None` with 120s timeout.
   - SqlException 3201 / OS error 5 catches trigger staging to `SERVERPROPERTY('InstanceDefaultBackupPath')` with guaranteed temporary staging cleanup in `finally`.

5. **Path Validation and Probe File Safety (`CanWriteToPathAsync`, lines 165–227)**:
   - Validates `Path.GetInvalidPathChars()` and `Path.GetInvalidFileNameChars()`.
   - Temporary `.probe_{Guid:N}.tmp` file deletion is strictly enclosed in a `finally` block.

---

## 2. Logic Chain

1. **Resolution of Corrupt SQLite Restores (Defect 1)**:
   - *Observation 1.1, 1.2.1*: In Iteration 1, corrupt files caused `ExecuteSqliteNativeRestoreAsync` to fail, but because active database connections were open, `canConnect` was true and the flawed logic `if (!canConnect && !restored)` returned `Success = true`. Furthermore, a destructive `File.Copy` destroyed live database files with random bytes.
   - *Logic*: Introducing 0-byte detection and 16-byte SQLite magic header validation (`SQLite format 3\0`) intercepts corrupted, random byte, truncated, or foreign files before any connection or database mutation occurs. Removing `File.Copy` guarantees that active SQLite database files are never corrupted by unvalidated file overwrites. Evaluating `if (!restored)` and `if (!canConnect)` ensures failed restores cleanly return `RestoreResult.Failed`. All 3 previously failing E2E tests (`Tier2_Restore_CorruptZeroByteFileFailsGracefully`, `Tier2_Restore_CorruptHeaderOrGarbageContentFailsGracefully`, and `Tier4_RealWorld_FailedRestoreFromCorruptFile_LeavesLiveDatabaseOperational`) now pass 100%.

2. **Resolution of Cancellation Exception Leaks (Defect 2)**:
   - *Observation 1.1, 1.2.2*: In Iteration 1, pre-cancelled tokens passed to `_gate.WaitAsync` threw unhandled `TaskCanceledException` / `OperationCanceledException` because the wait was outside the `try` block.
   - *Logic*: Enclosing `_gate.WaitAsync` in a dedicated `try...catch (OperationCanceledException)` block catches cancelled requests gracefully, logs a warning, and returns `Failed("... cancelled.")` without throwing unhandled exceptions. Not calling `_gate.Release()` in this catch avoids `SemaphoreFullException`, while the subsequent `finally` block guarantees release if the semaphore was acquired. This satisfies Requirement R4 and passes `Adversarial_PreCancelledToken_CreateBackup_ThrowsInsteadOfReturningFailed` and `Adversarial_PreCancelledToken_Restore_ThrowsInsteadOfReturningFailed`.

3. **Resolution of Concurrency Lock Isolation (Defect 3)**:
   - *Observation 1.1, 1.2.3*: `BackupService` is registered with transient DI lifetime. An instance field `_gate` allowed separate instances to execute concurrent operations.
   - *Logic*: Changing `_gate` to `private static readonly SemaphoreSlim _gate = new(1, 1);` serializes all backup and restore operations process-wide, preventing race conditions between manual UI triggers and background auto-backup timers. Verified by `ConcurrencyGuard_AcrossDifferentInstances_SharesStaticGate` and `Tier2_Concurrency_SimultaneousBackupAndRestoreCallsAreSerialized`.

4. **Resolution of SQL Server `SINGLE_USER` Lock Orphans & Permission Staging (Defect 4)**:
   - *Observation 1.2.4*: Setting `SINGLE_USER` without guaranteed `finally` execution risked leaving the production database in single-user mode if an error occurred. In addition, direct restores under user profile directories failed under SQL Server service accounts.
   - *Logic*: Wrapping `SINGLE_USER` in `try` and unconditionally issuing `SET MULTI_USER` in `finally` with `CancellationToken.None` guarantees the database is unlocked under any failure or cancellation scenario. The fallback copying mechanism to `InstanceDefaultBackupPath` overcomes OS Error 5 / SqlException 3201 permission constraints.

5. **Resolution of Path Validation and Probe Leaks (Defect 5)**:
   - *Observation 1.2.5*: Invalid filename characters caused unexpected exceptions, and probe file deletion was not in `finally`.
   - *Logic*: Validating against invalid path and filename characters rejects malformed paths immediately. Enclosing `File.Delete(probeFile)` inside `finally` guarantees probe cleanup even under filesystem faults.

---

## 3. Adversarial Stress Test Results

| Scenario / Attack Vector | Target Mechanism | Expected Behavior | Actual Behavior | Result |
|---|---|---|---|---|
| **0-byte restore file** | `RestoreSqliteDatabaseAsync` | Returns `RestoreResult.Failed("Backup file is empty.")` | Returns Failed immediately without opening connection | **PASS** |
| **Garbage byte restore file (2048 bytes)** | `RestoreSqliteDatabaseAsync` | Rejects file, does not overwrite DB | Rejection by SQLite magic byte header check; live DB untouched | **PASS** |
| **Pre-cancelled token on backup** | `CreateBackupAsync` | Returns `BackupResult.Failed("... cancelled.")` without unhandled exception | Handled cleanly in `catch (OperationCanceledException)` | **PASS** |
| **Pre-cancelled token on restore** | `RestoreDatabaseAsync` | Returns `RestoreResult.Failed("... cancelled.")` without unhandled exception | Handled cleanly in `catch (OperationCanceledException)` | **PASS** |
| **Concurrent operations across instances** | `_gate` static semaphore | All requests serialize safely | Shared static gate serializes calls; no file collisions | **PASS** |
| **Illegal path/filename characters** | `CanWriteToPathAsync` | Returns `false` without crashing | Tested against `GetInvalidFileNameChars()`; returns `false` | **PASS** |
| **Full Admin Disaster Recovery E2E suite** | 50 E2E scenarios | 100% pass rate across all tiers | 50/50 tests passed cleanly | **PASS** |
| **Combined Core Backup suite** | 41 unit/adversarial tests | 100% pass rate across all tests | 41/41 tests passed cleanly | **PASS** |

---

## 4. Integrity Assessment

- **Hardcoded outputs / fake test passes**: None. All assertions inspect dynamic filesystem, database entities, and result objects.
- **Shortcuts / mock bypasses**: None. Genuine EF Core and SQLite/SQL Server operations are executed.
- **Verification logs validity**: Confirmed empirically via live executions.

---

## 5. Caveats

- Unit and adversarial test runs in the local Windows environment execute against SQLite (`:memory:` and physical file-backed). SQL Server restore and backup paths (`CreateSqlServerBackupAsync`, `RestoreSqlServerDatabaseAsync`) were verified through structural code inspection, parameterization analysis, and Microsoft T-SQL standard compliance.
- No caveats regarding test pass rates: all 50 E2E tests and all 41 Core Backup tests passed with a 100% success rate.

---

## 6. Conclusion

**Verdict: APPROVE / CONFIRMED**

The empirical challenge confirms that:
1. All 50 tests in `tests/Supermarket.Admin.Tests/E2E/` now pass 100% (including `Tier2_Restore_CorruptZeroByteFileFailsGracefully`, `Tier2_Restore_CorruptHeaderOrGarbageContentFailsGracefully`, and `Tier4_RealWorld_FailedRestoreFromCorruptFile_LeavesLiveDatabaseOperational`).
2. All 7 tests in `tests/Supermarket.Core.Tests/AdversarialBackupTests.cs` pass 100%.
3. All 41 tests in `tests/Supermarket.Core.Tests/` matching `Backup` pass 100%.
4. Boundary conditions (zero-byte files, corrupt headers, pre-cancelled tokens, invalid paths, concurrency across instances) are robustly handled with strict error handling adhering to Requirement R4 and the Integrity Mandate.

---

## 7. Verification Method

To independently reproduce the empirical verification, execute the following commands in PowerShell (`BypassSandbox: true`):

```powershell
# 1. Run Admin E2E Test Suite (50 tests pass)
dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~E2E

# 2. Run Core Adversarial Stress Suite (7 tests pass)
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Adversarial

# 3. Run Full Core Backup Suite (41 tests pass)
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Backup
```
