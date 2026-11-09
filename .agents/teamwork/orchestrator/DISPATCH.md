## 2026-11-09T13:31:24Z

Add a production-ready "Database Backup & Recovery" tab to the Settings view in the Admin page of the Supermarket Management System using MVVM architecture, analyze the plan for architectural and safety flaws, and implement safe backup and recovery functionality.

Integrity mode: development

## Requirements

### R1. Architectural & Safety Flaw Analysis
Analyze the proposed plan and the existing database architecture for flaws, particularly regarding direct file-copying vs native database backup/restore commands, file locking when the database engine or EF Core contexts are active, connection releasing/pooling, and auto-backup scheduling. Deliver a concise flaw analysis report documenting these issues and the corrective design decisions prior to or alongside implementation.

### R2. UI Layout & Design (XAML)
Add a modern "Database Backup & Recovery" sub-page/tab to the Settings view in the Admin page, designed with clean cards or section dividers and fully optimized for right-to-left (RTL) Arabic localization:
- Manual Section: Include a prominent "إنشاء نسخة احتياطية الآن" (Backup Now) button that opens a SaveFileDialog to select the target backup destination, and a "استعادة قاعدة البيانات" (Restore Database) button that prompts the user with a safety confirmation dialog before proceeding.
- Auto-Backup Section: Include a ToggleSwitch for "التسجيل الاحتياطي التلقائي" (Enable Auto-Backup), a ComboBox for backup frequency ("يومي" - Daily, "أسبوعي" - Weekly), and a text box with a browse button combo to select the destination folder path.

### R3. ViewModel & Business Logic (C#)
- Implement all backup and restore operations using asynchronous commands (async/await) so the UI thread remains completely responsive during execution.
- Create an `IBackupService` interface and implementation responsible for safely executing database backup and restore operations, handling connection release and locking cleanly without failures when EF Core contexts are active.
- Expose status properties on the ViewModel to provide real-time UI feedback (e.g., "Last backup created successfully at [Timestamp]" or progress/status indicators).
- Implement an in-app background scheduler/timer that triggers automatic backups at the configured frequency while the Admin application is running, and persist all auto-backup settings across application restarts.

### R4. Safety, Error Handling & Logging
- Wrap all file input/output and database operations in strict try-catch blocks.
- Log failures locally and display a clean WPF modal warning dialog to the user whenever a backup or restore operation fails.
- During database restoration, ensure active connections are cleanly terminated or released so the restore does not fail or corrupt active state.

## Acceptance Criteria

### Flaw Analysis
- [ ] A concise flaw analysis document or section exists, explaining identified issues with the original plan (including file locking in active database engines and EF Core connection management) and detailing how the implementation resolves them.

### UI & RTL Localization
- [ ] The Settings view displays a "Database Backup & Recovery" tab with clean card/divider layout adhering to RTL Arabic layout conventions.
- [ ] Clicking "إنشاء نسخة احتياطية الآن" prompts a file save dialog allowing the user to choose the backup file path.
- [ ] Clicking "استعادة قاعدة البيانات" prompts a confirmation dialog before executing the restore.
- [ ] The Auto-Backup section allows toggling auto-backup on/off, selecting frequency ("يومي" or "أسبوعي"), and selecting a folder path.

### Backup & Restore Operations
- [ ] Triggering a manual backup produces a valid, restorable backup file at the selected path without freezing or blocking the UI.
- [ ] Restoring from a valid backup successfully replaces/restores the database state and completes without file lock or open EF Core context conflicts.
- [ ] Real-time status feedback updates on the UI upon backup completion with a success message and timestamp.
- [ ] Auto-backup settings persist across application restarts, and the in-app scheduler initiates backups according to the configured schedule when the Admin application is open.

### Safety & Error Handling
- [ ] Invalid backup paths, corrupt restore files, or aborted operations do not crash the application.
- [ ] Any failure during backup or restore logs the error and displays a modal warning dialog describing the issue.

### Verification
- [ ] Automated unit and/or integration tests exist and pass, verifying `IBackupService` backup creation, restore handling, and error conditions.
- [ ] The entire solution compiles cleanly with no build errors.


## 2026-11-09T13:38:56Z
Priority directive from user (received 2026-11-09T13:38:49Z):
"The user says 'rush on' — they want the team to move as fast as possible. Please prioritize speed and parallelize wherever you can."
Please incorporate this directive into scheduling and parallelization across subagents.
