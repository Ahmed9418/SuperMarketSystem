# Dispatch: Forensic Auditor for Milestone 1 (Core Backup & Settings Subsystem)

## Objective
Perform independent forensic integrity verification on Milestone 1 code:
- `src/Supermarket.Core/Interfaces/IBackupService.cs`
- `src/Supermarket.Core/Interfaces/BackupModels.cs`
- `src/Supermarket.Core/Interfaces/IBackupSettingsStore.cs`
- `src/Supermarket.Core/Services/BackupService.cs`
- `src/Supermarket.Core/Services/JsonBackupSettingsStore.cs`
- `src/Supermarket.Core/DependencyInjection/CoreServiceCollectionExtensions.cs`
- `tests/Supermarket.Core.Tests/BackupServiceTests.cs`

## Mandatory Reading
Read `ORIGINAL_REQUEST.md` at:
`C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\ORIGINAL_REQUEST.md`

## Forensic Integrity Checks
1. Hardcoded output check: Ensure `BackupResult` and `RestoreResult` are not returning hardcoded dummy results, and that tests do not assert against fabricated fixed responses without genuine execution.
2. Facade/dummy implementation check: Ensure `BackupService` executes real SQL Server T-SQL commands (`BACKUP DATABASE`, `RESTORE DATABASE`, `SINGLE_USER WITH ROLLBACK IMMEDIATE`, `MULTI_USER`, `ClearAllPools()`) and real SQLite backup (`BackupDatabase` / `VACUUM INTO`), and that `JsonBackupSettingsStore` performs genuine file I/O to `%LocalAppData%\SupermarketSystem\backup_settings.json`.
3. Check for external cheats, bypassed assertions, or mock stubbing of the primary subject under test.
4. Verify layout compliance with `PROJECT.md § Code Layout`.

## Verdict
Deliver a binary verdict in `handoff.md`:
- `CLEAN` (if 100% genuine implementation)
- `INTEGRITY VIOLATION: <details>` (if any cheating, facade, or hardcoding detected)
Target working directory:
`C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_auditor_m1\`
Send a message when finished.

## 2026-11-09T14:07:39Z
You are teamwork_auditor_m1.
Your working directory is:
C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_auditor_m1\

Read your dispatch instructions at:
C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_auditor_m1\DISPATCH.md

Mandatory reading:
C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\ORIGINAL_REQUEST.md
Worker M1 handoff at:
C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_worker_m1\handoff.md

Task:
Perform independent forensic integrity verification on Milestone 1 code:
- Check for hardcoded responses or dummy facades in IBackupService, BackupService, JsonBackupSettingsStore.
- Verify that SQL Server T-SQL commands (BACKUP DATABASE, RESTORE DATABASE, SINGLE_USER WITH ROLLBACK IMMEDIATE, ClearAllPools) and SQLite native backups are genuine.
- Verify that tests execute real business logic and do not bypass assertions.
- Deliver a binary verdict (CLEAN or INTEGRITY VIOLATION) with full evidence chain in handoff.md.
Send a message when finished.
