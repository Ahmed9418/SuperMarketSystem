# Dispatch: Worker M2 (Admin UI, ViewModel & Background Scheduler Subsystem)

## Objective
Implement Milestone 2: Modern "Database Backup & Recovery" tab in `SettingsView.xaml`, `ToggleSwitchStyle` in `Themes/Controls.xaml`, `SettingsViewModel.Backup.cs` with async commands and real-time status feedback, `AutoBackupScheduler.cs` with .NET 8 `PeriodicTimer`, DI container registration, and automated tests in `Supermarket.Admin.Tests`.

## Mandatory Reading
1. Read the original user request at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\ORIGINAL_REQUEST.md`
2. Read the project scope and interface contracts at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\PROJECT.md`
3. Read the flaw analysis at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\FLAW_ANALYSIS.md`
4. Read Explorer 2 & Explorer 3 reports at:
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_explorer_survey_2\handoff.md`
   `C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_explorer_survey_3\handoff.md`

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Write Ownership Boundaries
You exclusively own and may edit or create these files:
- `src/Supermarket.Admin/Themes/Controls.xaml`
- `src/Supermarket.Admin/Views/SettingsView.xaml`
- `src/Supermarket.Admin/ViewModels/SettingsViewModel.Backup.cs`
- `src/Supermarket.Admin/Services/IAutoBackupScheduler.cs`
- `src/Supermarket.Admin/Services/AutoBackupScheduler.cs`
- `src/Supermarket.Admin/App.xaml.cs`
- `tests/Supermarket.Admin.Tests/BackupAndRecoveryViewModelTests.cs`
DO NOT modify any files in `src/Supermarket.Core/`.

## Technical Specifications
1. **`Themes/Controls.xaml`**:
   - Add native WPF `ToggleSwitchStyle` for `CheckBox` controls featuring a modern sliding capsule track and thumb. Ensure the sliding track is isolated with `FlowDirection="LeftToRight"` for smooth horizontal sliding, with Arabic text content placed cleanly alongside.
2. **`SettingsView.xaml` Layout**:
   - Transform the primary container into a `TabControl` with 2 tabs:
     - Tab 1: "الإعدادات العامة والطباعة" (housing the existing general settings and printing content).
     - Tab 2: "النسخ الاحتياطي واستعادة البيانات" (housing the new Database Backup & Recovery UI).
   - In Tab 2, arrange 3 clean card sections with `CardBorderStyle` and Arabic RTL layout:
     - **Status Card (بطاقة حالة النظام والقاعدة)**: Displays database server/name, last backup time (`LastBackupDisplayTime`), status message (`BackupStatusMessage`), and busy progress bar (`IsBackupBusy`).
     - **Manual Section Card (النسخ الاحتياطي والاستعادة اليدوية)**:
       - Prominent "إنشاء نسخة احتياطية الآن" button (`PrimaryButtonStyle`) bound to `BackupNowCommand`.
       - "استعادة قاعدة البيانات" button (`DangerButtonStyle`) bound to `RestoreDatabaseCommand`.
     - **Auto-Backup Section Card (النسخ الاحتياطي التلقائي المجدول)**:
       - `CheckBox` with `Style="{StaticResource ToggleSwitchStyle}"` bound to `IsAutoBackupEnabled` with content "التسجيل الاحتياطي التلقائي".
       - Frequency `ComboBox` bound to `SelectedFrequency` with items "يومي" and "أسبوعي".
       - Destination folder `TextBox` with `Style="{StaticResource LatinInputStyle}"` bound to `AutoBackupFolderPath`, accompanied by a browse button bound to `BrowseBackupFolderCommand`.
       - Button "حفظ إعدادات النسخ التلقائي" bound to `SaveAutoBackupSettingsCommand`.
3. **`SettingsViewModel.Backup.cs`**:
   - Partial class extending `SettingsViewModel`.
   - Injected with `IBackupService`, `IBackupSettingsStore`, `IAutoBackupScheduler` (resolve or inject safely; maintain optional constructor parameter or DI resolution so existing 34 tests continue to construct cleanly).
   - Expose properties: `IsBackupBusy`, `BackupStatusMessage`, `IsAutoBackupEnabled`, `SelectedFrequency`, `AutoBackupFolderPath`, `LastBackupDisplayTime`, `BackupFrequencies`.
   - `BackupNowCommand`: Prompts SaveFileDialog (`Filter = "SQL Backup Files (*.bak)|*.bak|All Files (*.*)|*.*"`). If confirmed, executes `CreateBackupAsync`, updates `BackupStatusMessage` with success timestamp ("تم إنشاء النسخة الاحتياطية بنجاح في [الوقت]"), updates `LastBackupDisplayTime`. On failure, logs error and calls `_dialogs.ShowWarning(...)`.
   - `RestoreDatabaseCommand`: Prompts confirmation dialog via `_dialogs.Confirm(...)` before proceeding. If confirmed, prompts OpenFileDialog. If selected, executes `RestoreDatabaseAsync`, reloads settings, updates status. On failure, logs error and calls `_dialogs.ShowWarning(...)`.
   - `BrowseBackupFolderCommand`: Prompts folder picker (`OpenFolderDialog`), updates `AutoBackupFolderPath`.
   - `SaveAutoBackupSettingsCommand`: Saves settings via `IBackupSettingsStore.SaveAsync`, calls `_scheduler.ReloadSchedule(settings)`, updates status message.
4. **`AutoBackupScheduler.cs`**:
   - Implements `IAutoBackupScheduler` using .NET 8 `System.Threading.PeriodicTimer` on ThreadPool.
   - Detects missed backups on startup (`(DateTime.UtcNow - LastBackupUtc) >= interval`) and runs catch-up.
   - Periodically checks schedule, creates timestamped backup files, updates `LastBackupUtc`, and raises `AutoBackupCompleted` event.
   - Guarded against concurrency via `SemaphoreSlim(1, 1)`.
5. **DI Registration**:
   - In `App.xaml.cs`, register `IAutoBackupScheduler` as `Singleton`.
   - In `App.OnStartup`, start the scheduler `_ = scheduler.StartAsync();`.
6. **Unit Tests in `BackupAndRecoveryViewModelTests.cs`**:
   - Test manual backup command execution and real-time status update.
   - Test restore command confirmation flow and cancellation refusal.
   - Test auto-backup settings persistence and scheduler reload.
   - Verify `SettingsViewModel` resolves under `ValidateScopes = true` and `ValidateOnBuild = true`.

## Verification Commands
Run in PowerShell with `BypassSandbox: true`:
```powershell
dotnet build src/Supermarket.Admin/Supermarket.Admin.csproj --no-restore -p:NuGetAudit=false
dotnet test tests/Supermarket.Admin.Tests/Supermarket.Admin.Tests.csproj --no-restore -p:NuGetAudit=false
```

## Output
Write your handoff report to `handoff.md` in:
`C:\Users\20114\.gemini\antigravity\worktrees\SuperMarketSystem\teamwork_analysis\.agents\teamwork\teamwork_worker_m2\`
Send a completion message to the orchestrator.


## 2026-11-09T14:06:42Z
[Message] timestamp=2026-11-09T14:06:42Z sender=ed585109-e633-4496-b0a3-a5993f8ab64b priority=MESSAGE_PRIORITY_HIGH
Task: Implement Milestone 2
