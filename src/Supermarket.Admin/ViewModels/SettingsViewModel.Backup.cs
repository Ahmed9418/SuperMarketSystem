using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Supermarket.Admin.Services;
using Supermarket.Core.DependencyInjection;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Partial class extending <see cref="SettingsViewModel"/> to provide Database Backup &amp; Recovery
/// functionality, observable UI state properties, and asynchronous commands.
/// </summary>
public sealed partial class SettingsViewModel
{
    private readonly IBackupService? _backupService;
    private readonly IBackupSettingsStore? _backupSettingsStore;
    private readonly IAutoBackupScheduler? _scheduler;

    public Func<string?>? SaveFileDialogOverride { get; set; }
    public Func<string?>? OpenFileDialogOverride { get; set; }
    public Func<string?>? FolderDialogOverride { get; set; }

    [ObservableProperty]
    private bool isBackupBusy;

    [ObservableProperty]
    private string backupStatusMessage = "النظام جاهز للنسخ الاحتياطي";

    [ObservableProperty]
    private bool isAutoBackupEnabled;

    [ObservableProperty]
    private string selectedFrequency = "يومي";

    [ObservableProperty]
    private string autoBackupFolderPath = string.Empty;

    [ObservableProperty]
    private string lastBackupDisplayTime = "لم يتم إجراء نسخ احتياطي بعد";

    public IReadOnlyList<string> BackupFrequencies { get; } = new[] { "يومي", "أسبوعي" };

    public string DatabaseServerName =>
        string.IsNullOrWhiteSpace(SqlServerDiagnostics.ServerName) ? "Local Server" : SqlServerDiagnostics.ServerName;

    public string DatabaseCatalogName =>
        string.IsNullOrWhiteSpace(SqlServerDiagnostics.DatabaseName) ? "SupermarketDb" : SqlServerDiagnostics.DatabaseName;

    /// <summary>
    /// Extended constructor enabling DI injection of backup services while keeping compatibility
    /// with the primary constructor.
    /// </summary>
    public SettingsViewModel(
        IStoreSettingsService settings,
        IReportService reports,
        IDialogService dialogs,
        AuthSession session,
        IBackupService backupService,
        IBackupSettingsStore backupSettingsStore,
        IAutoBackupScheduler? scheduler = null)
        : this(settings, reports, dialogs, session)
    {
        _backupService = backupService;
        _backupSettingsStore = backupSettingsStore;
        _scheduler = scheduler;

        InitializeBackupSubsystem();
    }

    private void InitializeBackupSubsystem()
    {
        if (_scheduler != null)
        {
            _scheduler.AutoBackupCompleted += OnAutoBackupCompleted;
        }

        if (_backupSettingsStore != null)
        {
            _ = LoadBackupSettingsAsync();
        }
    }

    private async Task LoadBackupSettingsAsync()
    {
        if (_backupSettingsStore == null)
        {
            return;
        }

        try
        {
            var settings = await _backupSettingsStore.LoadAsync().ConfigureAwait(false);
            IsAutoBackupEnabled = settings.IsAutoBackupEnabled;
            SelectedFrequency = settings.Frequency == BackupFrequency.Weekly ? "أسبوعي" : "يومي";
            AutoBackupFolderPath = settings.DestinationFolderPath ?? string.Empty;

            if (settings.LastBackupUtc.HasValue)
            {
                LastBackupDisplayTime = settings.LastBackupUtc.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            }

            if (!string.IsNullOrWhiteSpace(settings.LastBackupStatus))
            {
                BackupStatusMessage = settings.LastBackupStatus;
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"تعذر استرجاع إعدادات النسخ الاحتياطي: {ex.Message}";
        }
    }

    private void OnAutoBackupCompleted(object? sender, BackupResult result)
    {
        void UpdateState()
        {
            if (result.Success)
            {
                var now = result.TimestampUtc?.ToLocalTime() ?? DateTime.Now;
                LastBackupDisplayTime = now.ToString("yyyy/MM/dd HH:mm:ss");
                BackupStatusMessage = $"تم إنشاء نسخة احتياطية مجدولة بنجاح في {now:yyyy/MM/dd HH:mm:ss}";
            }
            else
            {
                BackupStatusMessage = $"فشل النسخ الاحتياطي التلقائي: {result.ErrorMessage}";
            }
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(UpdateState);
        }
        else
        {
            UpdateState();
        }
    }

    /// <summary>
    /// Prompts the user for a destination file path and creates an immediate verified database backup.
    /// </summary>
    [RelayCommand]
    public async Task BackupNowAsync()
    {
        if (IsBackupBusy)
        {
            return;
        }

        if (_backupService == null)
        {
            _dialogs.ShowWarning("خدمة النسخ الاحتياطي غير متوفرة حالياً.", "تنبيه");
            return;
        }

        string? targetPath = null;
        if (SaveFileDialogOverride != null)
        {
            targetPath = SaveFileDialogOverride();
        }
        else
        {
            var defaultFileName = $"SupermarketDb_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var sfd = new SaveFileDialog
            {
                Title = "تحديد مسار حفظ النسخة الاحتياطية",
                Filter = "SQL Backup Files (*.bak)|*.bak|All Files (*.*)|*.*",
                FileName = defaultFileName,
                DefaultExt = ".bak"
            };

            if (sfd.ShowDialog() == true)
            {
                targetPath = sfd.FileName;
            }
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        IsBackupBusy = true;
        BackupStatusMessage = "جاري إنشاء النسخة الاحتياطية لقاعدة البيانات...";

        try
        {
            var result = await _backupService.CreateBackupAsync(targetPath);
            if (result.Success)
            {
                var now = DateTime.Now;
                LastBackupDisplayTime = now.ToString("yyyy/MM/dd HH:mm:ss");
                BackupStatusMessage = $"تم إنشاء النسخة الاحتياطية بنجاح في {now:yyyy/MM/dd HH:mm:ss}";

                if (_backupSettingsStore != null)
                {
                    var settings = await _backupSettingsStore.LoadAsync();
                    settings.LastBackupUtc = result.TimestampUtc ?? DateTime.UtcNow;
                    settings.LastBackupStatus = BackupStatusMessage;
                    await _backupSettingsStore.SaveAsync(settings);
                }

                _dialogs.ShowInformation($"تم إنشاء النسخة الاحتياطية بنجاح في:\n{targetPath}", "نجاح العملية");
            }
            else
            {
                BackupStatusMessage = $"فشل إنشاء النسخة الاحتياطية: {result.ErrorMessage}";
                _dialogs.ShowWarning(result.ErrorMessage ?? "حدث خطأ غير متوقع أثناء إنشاء النسخة الاحتياطية.", "فشل النسخ الاحتياطي");
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"خطأ: {ex.Message}";
            _dialogs.ShowWarning(ex.Message, "فشل النسخ الاحتياطي");
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    /// <summary>
    /// Displays a critical confirmation warning, prompts the user to select a backup file,
    /// and safely executes database restoration.
    /// </summary>
    [RelayCommand]
    public async Task RestoreDatabaseAsync()
    {
        if (IsBackupBusy)
        {
            return;
        }

        if (_backupService == null)
        {
            _dialogs.ShowWarning("خدمة استعادة البيانات غير متوفرة حالياً.", "تنبيه");
            return;
        }

        var confirmed = _dialogs.Confirm(
            "تحذير أمني هام:\nاستعادة قاعدة البيانات ستؤدي إلى استبدال كافة البيانات الحالية ببيانات النسخة الاحتياطية وإغلاق كافة الجلسات النشطة.\n\nهل أنت متأكد من المتابعة والاستعادة الآن؟",
            "تأكيد استعادة قاعدة البيانات");

        if (!confirmed)
        {
            return;
        }

        string? sourcePath = null;
        if (OpenFileDialogOverride != null)
        {
            sourcePath = OpenFileDialogOverride();
        }
        else
        {
            var ofd = new OpenFileDialog
            {
                Title = "اختر ملف النسخة الاحتياطية للاستعادة",
                Filter = "SQL Backup Files (*.bak)|*.bak|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog() == true)
            {
                sourcePath = ofd.FileName;
            }
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        IsBackupBusy = true;
        BackupStatusMessage = "جاري استعادة قاعدة البيانات وقطع الاتصالات النشطة...";

        try
        {
            var result = await _backupService.RestoreDatabaseAsync(sourcePath);
            if (result.Success)
            {
                var now = DateTime.Now;
                BackupStatusMessage = $"تمت استعادة قاعدة البيانات بنجاح في {now:yyyy/MM/dd HH:mm:ss}";

                await LoadAsync();

                _dialogs.ShowInformation("تمت استعادة قاعدة البيانات بنجاح وتحديث كافة البيانات في النظام.", "نجاح الاستعادة");
            }
            else
            {
                BackupStatusMessage = $"فشل استعادة قاعدة البيانات: {result.ErrorMessage}";
                _dialogs.ShowWarning(result.ErrorMessage ?? "حدث خطأ غير متوقع أثناء استعادة قاعدة البيانات.", "فشل الاستعادة");
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"خطأ: {ex.Message}";
            _dialogs.ShowWarning(ex.Message, "فشل الاستعادة");
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    /// <summary>
    /// Opens the modern folder picker dialog allowing the administrator to choose the destination folder.
    /// </summary>
    [RelayCommand]
    public void BrowseBackupFolder()
    {
        if (FolderDialogOverride != null)
        {
            var path = FolderDialogOverride();
            if (!string.IsNullOrWhiteSpace(path))
            {
                AutoBackupFolderPath = path;
            }
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "اختر مجلد حفظ النسخ الاحتياطية التلقائية"
        };

        if (dialog.ShowDialog() == true)
        {
            AutoBackupFolderPath = dialog.FolderName;
        }
    }

    /// <summary>
    /// Persists auto-backup configuration machine-locally and updates the background scheduler.
    /// </summary>
    [RelayCommand]
    public async Task SaveAutoBackupSettingsAsync()
    {
        if (IsBackupBusy)
        {
            return;
        }

        if (_backupSettingsStore == null)
        {
            _dialogs.ShowWarning("مخزن إعدادات النسخ الاحتياطي غير متوفر.", "تنبيه");
            return;
        }

        if (IsAutoBackupEnabled && string.IsNullOrWhiteSpace(AutoBackupFolderPath))
        {
            _dialogs.ShowWarning("يرجى اختيار مجلد حفظ النسخ الاحتياطية قبل تفعيل النسخ التلقائي.", "تنبيه مسار المجلد");
            return;
        }

        IsBackupBusy = true;
        try
        {
            var settings = await _backupSettingsStore.LoadAsync();
            settings.IsAutoBackupEnabled = IsAutoBackupEnabled;
            settings.Frequency = SelectedFrequency == "أسبوعي" ? BackupFrequency.Weekly : BackupFrequency.Daily;
            settings.DestinationFolderPath = AutoBackupFolderPath;

            await _backupSettingsStore.SaveAsync(settings);
            _scheduler?.ReloadSchedule(settings);

            BackupStatusMessage = $"تم حفظ إعدادات النسخ الاحتياطي التلقائي بنجاح في {DateTime.Now:HH:mm:ss}";
            _dialogs.ShowInformation("تم حفظ إعدادات النسخ الاحتياطي التلقائي بنجاح.", "نجاح الحفظ");
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"فشل حفظ الإعدادات: {ex.Message}";
            _dialogs.ShowWarning(ex.Message, "فشل الحفظ");
        }
        finally
        {
            IsBackupBusy = false;
        }
    }
}
