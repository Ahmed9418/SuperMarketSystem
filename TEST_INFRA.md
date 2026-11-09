# Test Infrastructure: Database Backup & Recovery Subsystem

## 1. Overview
This document specifies the test architecture, runner commands, and requirement traceability for the **Database Backup & Recovery** subsystem of the Supermarket Management System.

The test suite is structured as a **requirement-driven opaque-box test suite** covering Tiers 1 through 4. It exercises the public contracts, services, and domain workflows derived directly from `ORIGINAL_REQUEST.md`, `PROJECT.md`, and `FLAW_ANALYSIS.md`.

---

## 2. Test Runner Commands

All test runs require `dotnet test` with `--no-restore` and `-p:NuGetAudit=false`.

### Primary E2E Test Suite Runner
To execute all E2E and integration tests in the Admin test suite:
```powershell
dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~E2E
```

### Granular Tier Runner Commands
You can filter tests by specific tier using the test method naming convention:

- **Tier 1 (Feature Coverage — Manual Backup, Restore, Toggle, Frequency, Folder Path)**:
  ```powershell
  dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Tier1
  ```

- **Tier 2 (Boundary & Corner Cases — Corrupt Files, Invalid Paths, Concurrency, Empty Inputs)**:
  ```powershell
  dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Tier2
  ```

- **Tier 3 (Cross-Feature Combinations — Backup-Restore Roundtrips, Settings Survival)**:
  ```powershell
  dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Tier3
  ```

- **Tier 4 (Real-World Application Scenarios — Disaster Recovery, Scheduler Catch-Up, Cancellation)**:
  ```powershell
  dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Tier4
  ```

### By Test Suite File
- **Backup & Recovery Engine Suite**:
  ```powershell
  dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~BackupRecoveryE2ETests
  ```

- **Settings Persistence & Decoupling Suite**:
  ```powershell
  dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~BackupSettingsPersistenceTests
  ```

### Core Unit Test Suite Runner
To execute the unit tests in `Supermarket.Core.Tests`:
```powershell
dotnet test tests/Supermarket.Core.Tests/Supermarket.Core.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~BackupServiceTests
```

---

## 3. Test Architecture & Tier Methodology

The test suite enforces a 4-tier testing hierarchy ensuring end-to-end reliability, safety, and boundary resilience:

```
┌────────────────────────────────────────────────────────────────────────┐
│               Tier 4: Real-World Application Scenarios                │
│  - Full Admin Disaster Recovery (Catalog/Inventory Purge & Restoration)│
│  - Background Scheduler Catch-Up (Elapsed Time Backup on Startup)     │
│  - User Confirmation Cancellation (Safe Abort Without Modification)    │
│  - Post-Restore Change Tracking & Operational Integrity                │
│  - Corrupt Restore Failure Isolation (Live DB Left Intact)            │
├────────────────────────────────────────────────────────────────────────┤
│               Tier 3: Cross-Feature Combinations (Pairwise)            │
│  - Backup -> Mutate Products/Prices -> Restore -> Verify Reversion    │
│  - DB Restore Circular Wipeout Immunity (Settings in JSON Survive)     │
│  - Multi-Cycle Snapshot Isolation (Backup A -> Backup B -> Restore A)  │
│  - Active Query Concurrency During Backup Execution                    │
├────────────────────────────────────────────────────────────────────────┤
│               Tier 2: Boundary & Corner Cases                          │
│  - Corrupt File Headers / 0-Byte Payloads / Garbage Bytes              │
│  - Missing / Non-Existent Backup Files on Restore                      │
│  - Invalid Paths, Non-Existent Drives, Malformed Strings               │
│  - Corrupt JSON Settings Fallback to Safe Defaults                     │
│  - Concurrency Serialization via SemaphoreSlim(1, 1) Gate              │
├────────────────────────────────────────────────────────────────────────┤
│               Tier 1: Feature Coverage (>=5 Tests Per Feature)         │
│  - Manual Backup Creation (File Creation, Metadata, Output Validation) │
│  - Restore Execution Flow (Clean Restore, Connection Pool Clearing)    │
│  - Auto-Backup Toggle (Enable, Disable, Multi-Toggle Persistence)      │
│  - Backup Frequency (Daily, Weekly, Round-Trip Preservation)           │
│  - Destination Folder (Custom Paths, Arabic/Unicode Paths, Auto-Create)│
└────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Test Files & Responsibilities

| File Path | Scope & Purpose |
|---|---|
| `tests/Supermarket.Admin.Tests/E2E/BackupRecoveryE2ETests.cs` | Comprehensive E2E tests covering `IBackupService` operations, restore workflows, database state mutation & reversion, disaster recovery, boundary conditions, and scheduler catch-up. |
| `tests/Supermarket.Admin.Tests/E2E/BackupSettingsPersistenceTests.cs` | Isolated tests verifying `IBackupSettingsStore` (`JsonBackupSettingsStore`), JSON configuration persistence, auto-backup toggle, frequency enum mapping, directory handling, and database restore isolation. |

---

## 5. Requirement & Flaw Traceability Matrix

| Requirement / Flaw ID | System Component | Test Coverage Method(s) |
|---|---|---|
| **R1 / Flaw 1** (Native Commands vs File Copy) | `BackupService.CreateBackupAsync` | `Tier1_ManualBackup_CreatesPhysicalFileWithNonZeroSize`<br>`Tier3_CrossFeature_MultiCycleBackupAndRestoreIntegrity` |
| **R1 / Flaw 2** (Restore Exclusivity & Pool Clearing) | `BackupService.RestoreDatabaseAsync` | `Tier1_Restore_ClearsConnectionPoolsAndAllowsNewQueries`<br>`Tier1_Restore_SucceedsWhenActiveQueriesExist` |
| **R1 / Flaw 3** (Circular Wipeout Hazard) | `JsonBackupSettingsStore` | `Tier3_CrossFeature_AutoBackupSettings_SurviveDatabaseRestore_CircularWipeoutImmunity`<br>`Tier1_AutoBackupToggle_PersistsAcrossInstances` |
| **R1 / Flaw 4** (UI Threading & Concurrency) | `SemaphoreSlim(1,1)` | `Tier2_Concurrency_SimultaneousBackupAndRestoreCallsAreSerialized`<br>`Tier2_Settings_ConcurrentLoadsAndSavesAreThreadSafe` |
| **R2** (Manual Backup Now) | `BackupNowCommand` / `CreateBackupAsync` | `Tier1_ManualBackup_ReturnsSuccessResultWithValidMetadata`<br>`Tier1_ManualBackup_AutoCreatesMissingParentDirectory` |
| **R2** (Restore Database Safety Prompt) | `RestoreDatabaseCommand` / `Confirm` | `Tier4_RealWorld_UserCancellationScenario_DatabaseRemainsUntouched`<br>`Tier1_Restore_ValidBackupRestoresSuccessfully` |
| **R2** (Auto-Backup Toggle & Frequency) | `BackupSettings` / Settings UI | `Tier1_AutoBackupToggle_*`<br>`Tier1_FrequencySelection_*` |
| **R2** (Destination Folder Path) | `DestinationFolderPath` | `Tier1_DestinationFolder_*`<br>`Tier2_Backup_CanWriteToPath_ValidAndInvalidPaths` |
| **R3** (Async Execution & Scheduler Catch-Up) | `IAutoBackupScheduler` / Logic | `Tier4_RealWorld_SchedulerCatchUp_MissedBackupTriggeredOnLaunch`<br>`Tier4_RealWorld_SchedulerCatchUp_NotDue_DoesNotTrigger` |
| **R4** (Safety, Modal Warnings, Corrupt Files) | Try-catch & `RestoreResult` | `Tier2_Restore_CorruptZeroByteFileFailsGracefully`<br>`Tier2_Restore_CorruptHeaderOrGarbageContentFailsGracefully`<br>`Tier4_RealWorld_FailedRestoreFromCorruptFile_LeavesLiveDatabaseOperational` |

---

## 6. Test Environment & Harness Design

1. **Relational Database Engine**: Tests run against SQLite test fixtures (`SqliteTestDatabase`, `TestContextFactory`) mirroring full relational constraints, foreign keys, cascade rules, and transactions without requiring external SQL Server local instances on CI agents.
2. **Settings Isolation**: Every test instantiates `JsonBackupSettingsStore` targeting an isolated temporary file (`Path.GetTempFileName()`) cleaned up in test `Dispose()`, ensuring tests never contaminate or read the operator's actual `%LocalAppData%\SupermarketSystem\backup_settings.json`.
3. **Deterministic Verification**: Tests populate deterministic seed data (products, categories, stock, store settings) and verify observable outputs directly against expected invariants.
