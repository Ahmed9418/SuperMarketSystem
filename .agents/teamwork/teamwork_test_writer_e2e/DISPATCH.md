# Dispatch: E2E Test Writer (Requirement-Driven Opaque-Box Test Suite)

## Objective
Design and implement the E2E and integration test suite for the Database Backup & Recovery subsystem derived strictly from user requirements (`ORIGINAL_REQUEST.md`), without depending on implementation internals. Create `TEST_INFRA.md` and publish `TEST_READY.md`.

## Mandatory Reading
1. Read the original user request at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\ORIGINAL_REQUEST.md`
2. Read the project scope and feature inventory at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\PROJECT.md`
3. Read the flaw analysis document at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\FLAW_ANALYSIS.md`

## Mandatory Integrity Warning
DO NOT CHEAT. All test cases and assertions must be genuine. DO NOT hardcode test results, create dummy assertions, or circumvent the intended verification. A teamwork_auditor will independently verify your work.

## Write Ownership Boundaries
You exclusively own and may edit or create these files:
- `TEST_INFRA.md` (at project root)
- `TEST_READY.md` (at project root)
- `tests/Supermarket.Admin.Tests/E2E/BackupRecoveryE2ETests.cs`
- `tests/Supermarket.Admin.Tests/E2E/BackupSettingsPersistenceTests.cs`
DO NOT modify any production source files in `src/`.

## Test Suite Methodology (4 Tiers)
Follow the 4-tier methodology:
1. **Tier 1 - Feature Coverage (>=5 tests per feature)**:
   - Happy path tests for: Manual Backup creation, Restore execution flow, Auto-Backup toggle, Backup frequency selection ("يومي" / "أسبوعي"), Destination folder selection.
2. **Tier 2 - Boundary & Corner Cases (>=5 tests per feature)**:
   - Non-existent restore paths, corrupt backup file headers, invalid characters in destination paths, read-only destination directory, empty path strings, concurrent triggers.
3. **Tier 3 - Cross-Feature Combinations (Pairwise)**:
   - Create backup -> modify store data -> execute restore -> assert store data reverted.
   - Configure auto-backup settings (enable + weekly + custom folder) -> execute restore from older backup -> assert auto-backup settings in local JSON remain unchanged!
   - Toggle auto-backup on/off -> change frequency -> verify persistence across fresh instances.
4. **Tier 4 - Real-World Application Scenarios (>=5 application tests)**:
   - Complete admin disaster recovery scenario: create backup, delete products/records, restore backup, verify full product catalog restored.
   - Background scheduler catch-up scenario: app launched after scheduled backup elapsed time -> triggers missed backup automatically.
   - Cancellation scenario: user aborts confirmation dialog -> database remains untouched.

## Test Infrastructure Artifacts
1. **`TEST_INFRA.md`**:
   Document the test runner command (`dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~E2E`), test architecture, and feature mapping.
2. **`TEST_READY.md`**:
   Publish when test files are written and compile-ready, summarizing test count per tier and runner commands.

## Verification
Ensure test files compile and pass:
`dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~E2E`
(Use `BypassSandbox: true` when running dotnet test commands).

## Output
Write your handoff report to `handoff.md` in:
`C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_test_writer_e2e\`
Send a completion message to the orchestrator.

## 2026-11-09T13:52:51Z
Received dispatch from parent ed585109-e633-4496-b0a3-a5993f8ab64b.
Task: Design and implement the comprehensive requirement-driven opaque-box test suite for Database Backup & Recovery covering Tiers 1-4.
