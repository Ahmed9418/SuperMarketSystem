# Milestone 1 Adversarial Challenge Report: Boundary, Corruption & Safety Probes

**Agent**: `teamwork_challenger_m1_2`  
**Milestone**: Milestone 1 (Core Backup & Settings Subsystem in `Supermarket.Core`)  
**Verdict**: **REJECT** (Critical and High vulnerabilities confirmed empirically)  
**Date**: 2026-11-09T14:32:00Z  

---

## 1. Observation

1. **Test Execution & Empirical Failures**:
   - Command executed:
     ```powershell
     dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Adversarial
     ```
   - Verbatim Output (Failed: 5, Passed: 2, Total: 7):
     ```
     [xUnit.net 00:00:20.19] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_PreCancelledToken_Restore_ThrowsInsteadOfReturningFailed [FAIL]
     Actual: System.Threading.Tasks.TaskCanceledException: A task was canceled.
        at Supermarket.Core.Services.BackupService.RestoreDatabaseAsync(String backupFilePath, CancellationToken cancellationToken) in C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\src\Supermarket.Core\Services\BackupService.cs:line 109
     
     [xUnit.net 00:00:20.46] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_PreCancelledToken_CreateBackup_ThrowsInsteadOfReturningFailed [FAIL]
     Actual: System.Threading.Tasks.TaskCanceledException: A task was canceled.
        at Supermarket.Core.Services.BackupService.CreateBackupAsync(String destinationFilePath, CancellationToken cancellationToken) in C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\src\Supermarket.Core\Services\BackupService.cs:line 58
     
     [xUnit.net 00:00:21.28] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_PhysicalDb_EmpiricalResult [FAIL]
     BUG CONFIRMED: BackupService reported Success=true on physical DB when restoring a corrupt file! ErrorMessage: ''
     
     [xUnit.net 00:00:21.36] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreZeroByteFile_MustReturnFailed [FAIL]
     BUG CONFIRMED: Restoring from a 0-byte file reported Success=true! ErrorMessage: ''
     
     [xUnit.net 00:00:21.47] Supermarket.Core.Tests.AdversarialBackupTests.Adversarial_RestoreCorruptFile_InMemoryDb_EmpiricalResult [FAIL]
     BUG CONFIRMED: BackupService reported Success=true when restoring a corrupt file containing random bytes! ErrorMessage: ''
     ```

2. **Source Code Flaw in `RestoreSqliteDatabaseAsync` (`src/Supermarket.Core/Services/BackupService.cs`, lines 506–533)**:
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

3. **Source Code Flaw in Semaphore Wait Cancellation (`src/Supermarket.Core/Services/BackupService.cs`, lines 58 & 109)**:
   - Line 58:
     ```csharp
     await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
     try
     {
     ```
   - Line 109:
     ```csharp
     await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
     try
     {
     ```
   `_gate.WaitAsync(cancellationToken)` sits outside the `try ... catch (OperationCanceledException)` blocks.

4. **Source Code Flaw in `CanWriteToPathAsync` (`src/Supermarket.Core/Services/BackupService.cs`, lines 158–184)**:
   ```csharp
   string? dir = null;
   if (Directory.Exists(destinationPath))
   {
       dir = destinationPath;
   }
   else
   {
       dir = Path.GetDirectoryName(destinationPath);
       if (string.IsNullOrWhiteSpace(dir))
       {
           dir = Directory.GetCurrentDirectory();
       }
   }
   ...
   var probeFile = Path.Combine(dir, $".probe_{Guid.NewGuid():N}.tmp");
   ```
   If `destinationPath` contains illegal filename characters (e.g., `invalid|file<name>?.bak`), it extracts the valid parent directory, writes a probe file `.probe_*.tmp` there, and returns `true`, completely ignoring the illegal filename.

5. **Asymmetry in SQL Server Restore vs Backup (`src/Supermarket.Core/Services/BackupService.cs`, lines 340–346 vs 249–289)**:
   In `CreateSqlServerBackupAsync`, when direct backup fails due to OS Error 5 (SQL Server service account lacking permissions to user directories), it falls back to staging in `SERVERPROPERTY('InstanceDefaultBackupPath')`. In `RestoreSqlServerDatabaseAsync`, no staging fallback exists: it passes `@path` directly to `RESTORE DATABASE`, which will fail when restoring from user folders (Desktop, Downloads, Documents).

---

## 2. Logic Chain

1. **Silent False Success on Corrupted Backup Files (Observation 1 & 2)**:
   - When a corrupted backup file (e.g. 1024/2048 bytes of random binary noise or 0 bytes) is passed to `RestoreDatabaseAsync`, `ExecuteSqliteNativeRestoreAsync` catches the SQLite format exception and returns `restored = false`.
   - On line 527, the check is `if (!canConnect && !restored)`.
   - Because the active database connection was still open and unaffected, `context.Database.CanConnectAsync()` returns `true`.
   - `!canConnect` evaluates to `false`. Therefore `(!canConnect && !restored)` evaluates to `false && true == false`.
   - Execution falls through to line 533: `return RestoreResult.Succeeded();`.
   - **Impact**: The UI/Admin will report "Database restored successfully!" while the database was never touched and the restore completely failed.

2. **Active Database Overwrite / Data Destruction Risk (Observation 2)**:
   - On lines 508–522, `BackupService` executes `File.Copy(backupFilePath, dataSource, overwrite: true);` even when `restored == false`!
   - If the file is corrupted, it attempts to overwrite the active database file with random noise *before* validating the file header.
   - On Windows, this threw an `IOException` only because `context.Database.GetDbConnection()` was opened at line 504. If connection pooling or operating system file semantics permitted the copy, the active database file would be permanently destroyed with random noise.

3. **Unhandled Cancellation Crash (Observation 1 & 3)**:
   - When a cancellation token is cancelled before or during the semaphore wait, `_gate.WaitAsync(cancellationToken)` immediately throws `TaskCanceledException` / `OperationCanceledException`.
   - Because lines 58 and 109 are positioned *outside* the `try-catch` blocks, the exception is not caught and escapes the method unhandled.
   - This directly violates Requirement R4: *"Wrap all file input/output and database operations in strict try-catch blocks... Invalid backup paths, corrupt restore files, or aborted operations do not crash the application."*

4. **False Positive Writability on Illegal Filenames (Observation 4)**:
   - `CanWriteToPathAsync` only probes the directory by creating `.probe_<guid>.tmp`.
   - It never validates the filename portion against `Path.GetInvalidFileNameChars()`.
   - Calling `CanWriteToPathAsync` with `C:\temp\bad|file<name>?.bak` returns `true`.

---

## 3. Caveats

- Tests were run against SQLite in both in-memory and physical file-backed modes. Microsoft SQL Server testing was based on source code analysis of `BackupService.cs` (lines 227–368); an external SQL Server instance was not connected during this test run.
- The connection pooling and file unlock verification (`Adversarial_RestoreLocksAndConnectionPool_CanDeleteBackupAfterRestore`) passed successfully; `SqliteConnection.ClearAllPools()` effectively releases file locks after successful operations.

---

## 4. Conclusion

**Verdict: REJECT Milestone 1.**

The Milestone 1 core implementation cannot be approved in its current state due to critical flaws in failure handling:
1. **Critical Bug**: `RestoreDatabaseAsync` reports `Success = true` on corrupt (random bytes / 0 bytes) files.
2. **Critical Bug**: `RestoreSqliteDatabaseAsync` attempts direct `File.Copy` of unverified backup files over the production database.
3. **Critical Bug**: `CreateBackupAsync` and `RestoreDatabaseAsync` crash with unhandled `TaskCanceledException` on cancelled tokens.
4. **High Bug**: `CanWriteToPathAsync` returns `true` for invalid filename characters.
5. **High Bug**: `RestoreSqlServerDatabaseAsync` lacks staging directory fallback for access-restricted paths.

### Required Actions for Worker M1:
1. In `RestoreSqliteDatabaseAsync`:
   - Remove the unverified `File.Copy` over the active database file.
   - If `!restored`, immediately return `RestoreResult.Failed("Backup file is corrupt or not a valid database.")`.
   - Validate SQLite header magic bytes (`SQLite format 3\0`) prior to attempting restore.
2. In `CreateBackupAsync` and `RestoreDatabaseAsync`:
   - Place `_gate.WaitAsync(cancellationToken)` inside a try block or catch `OperationCanceledException` around it to return `BackupResult.Failed("Backup operation was cancelled.")` / `RestoreResult.Failed("Restore operation was cancelled.")`.
3. In `CanWriteToPathAsync`:
   - Validate `Path.GetInvalidFileNameChars()` and filename validity.
4. In `RestoreSqlServerDatabaseAsync`:
   - Implement staging fallback (copying to SQL Server default backup path) when SQL Server encounters permission errors accessing user folders.

---

## 5. Verification Method

To verify these findings, run:

```powershell
# Run the empirical adversarial test suite
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Adversarial
```

Files to inspect:
- `src/Supermarket.Core/Services/BackupService.cs` (lines 58, 109, 158–184, 506–533)
- `tests/Supermarket.Core.Tests/AdversarialBackupTests.cs`
