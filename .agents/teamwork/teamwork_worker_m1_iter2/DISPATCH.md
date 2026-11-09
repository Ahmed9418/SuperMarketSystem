# Dispatch: Worker M1 Iteration 2 (Core Backup Remediation Worker)

## Objective
Remediate the 5 specific defects in `src/Supermarket.Core/Services/BackupService.cs` identified by Reviewers, Challengers, and E2E Test Suite in Iteration 1, ensuring 100% pass across all unit, adversarial, and E2E test suites.

## Mandatory Reading
1. Read `ORIGINAL_REQUEST.md` at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\ORIGINAL_REQUEST.md`
2. Read `PROJECT.md` at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\PROJECT.md`
3. Read `FLAW_ANALYSIS.md` at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\FLAW_ANALYSIS.md`
4. Read Gate status and review reports at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\orchestrator\GATE_STATUS.md`
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_reviewer_m1_2\handoff.md`
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_challenger_m1_2\handoff.md`
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\TEST_READY.md`

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Write Ownership Boundaries
You exclusively own and may edit or create these files:
- `src/Supermarket.Core/Services/BackupService.cs`
- `src/Supermarket.Core/DependencyInjection/CoreServiceCollectionExtensions.cs`
- `tests/Supermarket.Core.Tests/BackupServiceTests.cs`
- `tests/Supermarket.Core.Tests/AdversarialBackupTests.cs`
DO NOT modify any other files.

## Required Remediations
1. **Fix Corrupt Restore in `RestoreSqliteDatabaseAsync`**:
   - Validate file existence and length (`if (new FileInfo(backupFilePath).Length == 0) return RestoreResult.Failed("Backup file is empty.");`).
   - Validate SQLite header magic bytes (`SQLite format 3\0`, ASCII bytes `53 51 4C 69 74 65 20 66 6F 72 6D 61 74 20 33 00`) before attempting any restore. If the header does not match, return `RestoreResult.Failed("Backup file is corrupt or not a valid database.");`.
   - Remove unverified direct `File.Copy` over the active database file (preventing corruption of live databases).
   - If `!restored`, return `RestoreResult.Failed("Failed to restore SQLite database from backup file.");`.
   - Check `canConnect` only after successful restore. If `!canConnect`, return `RestoreResult.Failed("Could not verify database connectivity after restore.");`.
2. **Fix Unhandled Cancellation**:
   - In `CreateBackupAsync` and `RestoreDatabaseAsync`, wrap the semaphore `_gate.WaitAsync(cancellationToken)` in `try...catch (OperationCanceledException)` (or place within the main try block) and return `BackupResult.Failed("Backup operation was cancelled.")` / `RestoreResult.Failed("Restore operation was cancelled.")` without letting unhandled exceptions crash callers.
3. **Make Concurrency Gate Static**:
   - Change `private readonly SemaphoreSlim _gate = new(1, 1);` to `private static readonly SemaphoreSlim _gate = new(1, 1);` in `BackupService.cs`.
4. **SQL Server `SINGLE_USER` try-finally & Staging Fallback**:
   - Enclose `ALTER DATABASE ... SET SINGLE_USER` in a `try...finally` block that guarantees resetting to `SET MULTI_USER`.
   - In `RestoreSqlServerDatabaseAsync`, add staging fallback (copying to SQL Server default backup path or temp folder) if direct restore fails due to OS Error 5 (service account permissions).
5. **Path Validation in `CanWriteToPathAsync`**:
   - Validate the filename against `Path.GetInvalidFileNameChars()` and `Path.GetInvalidPathChars()`. If invalid characters exist, return `false`.
   - Ensure probe file deletion is wrapped in a `finally` block.

## Verification Commands
Run in PowerShell with `BypassSandbox: true`:
```powershell
dotnet build src/Supermarket.Core/Supermarket.Core.csproj --no-restore -p:NuGetAudit=false
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Backup
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Adversarial
dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~E2E
```
All tests must pass at 100%!

## Output
Write your findings and test results to `handoff.md` in:
`C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_worker_m1_iter2\`
Send a completion message to the orchestrator.


## 2026-11-09T14:32:17Z
You are teamwork_worker_m1_iter2.
Your working directory is:
C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_worker_m1_iter2\

Remediate the 5 specific defects in src/Supermarket.Core/Services/BackupService.cs:
1. RestoreSqliteDatabaseAsync: Validate file length > 0 and SQLite header magic bytes before restore; remove unverified File.Copy over active database; if !restored return Failed; verify canConnect only after restore.
2. Catch OperationCanceledException around _gate.WaitAsync in CreateBackupAsync and RestoreDatabaseAsync and return BackupResult.Failed/RestoreResult.Failed without crashing.
3. Make _gate static: private static readonly SemaphoreSlim _gate = new(1, 1);
4. Wrap ALTER DATABASE ... SET SINGLE_USER in try-finally ensuring MULTI_USER reset, and add restore staging fallback for user folders.
5. In CanWriteToPathAsync, validate against invalid filename characters and put probe file deletion in finally.
Verify with dotnet test across Backup, Adversarial, and E2E test suites (100% pass). Write handoff.md and report back when finished.
