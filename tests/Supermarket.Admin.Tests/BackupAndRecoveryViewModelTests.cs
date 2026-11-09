using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Admin.Services;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.DependencyInjection;
using Supermarket.Core.Interfaces;
using Xunit;

namespace Supermarket.Admin.Tests;

public sealed class BackupAndRecoveryViewModelTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task BackupNowCommand_Success_CreatesBackup_UpdatesStatusAndDisplayTime()
    {
        _fixture.SignInAsAdmin();
        var backupService = new TestBackupService { ShouldSucceed = true };
        var settingsStore = new TestBackupSettingsStore();
        var scheduler = new TestAutoBackupScheduler();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            backupService,
            settingsStore,
            scheduler);

        var targetFile = @"C:\Backups\Supermarket_Test.bak";
        vm.SaveFileDialogOverride = () => targetFile;

        // Act
        await vm.BackupNowCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(backupService.CreatedBackups);
        Assert.Equal(targetFile, backupService.CreatedBackups[0]);
        Assert.Contains("تم إنشاء النسخة الاحتياطية بنجاح", vm.BackupStatusMessage);
        Assert.NotEqual("لم يتم إجراء نسخ احتياطي بعد", vm.LastBackupDisplayTime);
        Assert.Single(_fixture.Dialogs.Informations);
        Assert.Empty(_fixture.Dialogs.Warnings);

        // Assert settings store was updated with last backup timestamp
        Assert.NotNull(settingsStore.CurrentSettings.LastBackupUtc);
    }

    [Fact]
    public async Task BackupNowCommand_Failure_ShowsWarning_UpdatesStatusWithErrorMessage()
    {
        _fixture.SignInAsAdmin();
        var backupService = new TestBackupService
        {
            ShouldSucceed = false,
            ErrorMessage = "Disk full: cannot write backup"
        };
        var settingsStore = new TestBackupSettingsStore();
        var scheduler = new TestAutoBackupScheduler();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            backupService,
            settingsStore,
            scheduler);

        vm.SaveFileDialogOverride = () => @"C:\Backups\Failed.bak";

        // Act
        await vm.BackupNowCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("فشل إنشاء النسخة الاحتياطية", vm.BackupStatusMessage);
        Assert.Contains("Disk full", vm.BackupStatusMessage);
        Assert.Single(_fixture.Dialogs.Warnings);
        Assert.Contains("Disk full", _fixture.Dialogs.Warnings[0]);
    }

    [Fact]
    public async Task BackupNowCommand_UserCancelsDialog_DoesNotInvokeBackupService()
    {
        _fixture.SignInAsAdmin();
        var backupService = new TestBackupService();
        var settingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            backupService,
            settingsStore);

        // User cancelled file dialog
        vm.SaveFileDialogOverride = () => null;

        // Act
        await vm.BackupNowCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(backupService.CreatedBackups);
        Assert.Empty(_fixture.Dialogs.Informations);
        Assert.Empty(_fixture.Dialogs.Warnings);
    }

    [Fact]
    public async Task RestoreDatabaseCommand_UserConfirmsAndSelectsFile_ExecutesRestoreAndReloads()
    {
        _fixture.SignInAsAdmin();
        var backupService = new TestBackupService { ShouldSucceed = true };
        var settingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            backupService,
            settingsStore);

        _fixture.Dialogs.ConfirmResult = true;
        var restorePath = @"C:\Backups\ValidSnapshot.bak";
        vm.OpenFileDialogOverride = () => restorePath;

        // Act
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(backupService.RestoredBackups);
        Assert.Equal(restorePath, backupService.RestoredBackups[0]);
        Assert.Contains("تمت استعادة قاعدة البيانات بنجاح", vm.BackupStatusMessage);
        Assert.Single(_fixture.Dialogs.Informations);
        Assert.Empty(_fixture.Dialogs.Warnings);
    }

    [Fact]
    public async Task RestoreDatabaseCommand_UserRefusesConfirmation_AbortsWithoutPromptingOrRestoring()
    {
        _fixture.SignInAsAdmin();
        var backupService = new TestBackupService();
        var settingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            backupService,
            settingsStore);

        // User denies confirmation dialog
        _fixture.Dialogs.ConfirmResult = false;
        var openDialogInvoked = false;
        vm.OpenFileDialogOverride = () =>
        {
            openDialogInvoked = true;
            return @"C:\Backups\NeverUsed.bak";
        };

        // Act
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);

        // Assert
        Assert.False(openDialogInvoked, "OpenFileDialog should not be displayed when user declines confirmation");
        Assert.Empty(backupService.RestoredBackups);
    }

    [Fact]
    public async Task RestoreDatabaseCommand_UserCancelsFileDialog_AbortsWithoutCallingRestoreService()
    {
        _fixture.SignInAsAdmin();
        var backupService = new TestBackupService();
        var settingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            backupService,
            settingsStore);

        _fixture.Dialogs.ConfirmResult = true;
        vm.OpenFileDialogOverride = () => null; // Cancelled

        // Act
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(backupService.RestoredBackups);
        Assert.Empty(_fixture.Dialogs.Informations);
        Assert.Empty(_fixture.Dialogs.Warnings);
    }

    [Fact]
    public async Task RestoreDatabaseCommand_Failure_ShowsWarning_UpdatesStatusWithErrorMessage()
    {
        _fixture.SignInAsAdmin();
        var backupService = new TestBackupService
        {
            ShouldSucceed = false,
            ErrorMessage = "Database file corrupted"
        };
        var settingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            backupService,
            settingsStore);

        _fixture.Dialogs.ConfirmResult = true;
        vm.OpenFileDialogOverride = () => @"C:\Backups\Corrupt.bak";

        // Act
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("فشل استعادة قاعدة البيانات", vm.BackupStatusMessage);
        Assert.Contains("Database file corrupted", vm.BackupStatusMessage);
        Assert.Single(_fixture.Dialogs.Warnings);
    }

    [Fact]
    public void BrowseBackupFolderCommand_WithFolderSelected_UpdatesAutoBackupFolderPath()
    {
        _fixture.SignInAsAdmin();
        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            new TestBackupService(),
            new TestBackupSettingsStore());

        vm.FolderDialogOverride = () => @"D:\SupermarketAutoBackups";

        // Act
        vm.BrowseBackupFolderCommand.Execute(null);

        // Assert
        Assert.Equal(@"D:\SupermarketAutoBackups", vm.AutoBackupFolderPath);
    }

    [Fact]
    public async Task SaveAutoBackupSettingsCommand_EnabledWithEmptyFolder_ShowsWarningAndDoesNotSave()
    {
        _fixture.SignInAsAdmin();
        var settingsStore = new TestBackupSettingsStore();
        var scheduler = new TestAutoBackupScheduler();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            new TestBackupService(),
            settingsStore,
            scheduler);

        vm.IsAutoBackupEnabled = true;
        vm.AutoBackupFolderPath = "   "; // Invalid empty

        // Act
        await vm.SaveAutoBackupSettingsCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(_fixture.Dialogs.Warnings);
        Assert.Empty(scheduler.ReloadScheduleCalls);
    }

    [Fact]
    public async Task SaveAutoBackupSettingsCommand_ValidInput_PersistsSettingsAndReloadsScheduler()
    {
        _fixture.SignInAsAdmin();
        var settingsStore = new TestBackupSettingsStore();
        var scheduler = new TestAutoBackupScheduler();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            new TestBackupService(),
            settingsStore,
            scheduler);

        vm.IsAutoBackupEnabled = true;
        vm.SelectedFrequency = "أسبوعي";
        vm.AutoBackupFolderPath = @"C:\Backups\Weekly";

        // Act
        await vm.SaveAutoBackupSettingsCommand.ExecuteAsync(null);

        // Assert
        Assert.True(settingsStore.CurrentSettings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Weekly, settingsStore.CurrentSettings.Frequency);
        Assert.Equal(@"C:\Backups\Weekly", settingsStore.CurrentSettings.DestinationFolderPath);

        Assert.Single(scheduler.ReloadScheduleCalls);
        Assert.Equal(BackupFrequency.Weekly, scheduler.ReloadScheduleCalls[0].Frequency);
        Assert.Contains("تم حفظ إعدادات", vm.BackupStatusMessage);
        Assert.Single(_fixture.Dialogs.Informations);
    }

    [Fact]
    public void AutoBackupScheduler_AutoBackupCompletedEvent_UpdatesViewModelState()
    {
        _fixture.SignInAsAdmin();
        var scheduler = new TestAutoBackupScheduler();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            new TestBackupService(),
            new TestBackupSettingsStore(),
            scheduler);

        var timestamp = new DateTime(2026, 11, 9, 14, 30, 0, DateTimeKind.Utc);
        var result = BackupResult.Succeeded(@"C:\Backups\Auto.bak", 1024, timestamp);

        // Act: raise event from scheduler
        scheduler.RaiseCompleted(result);

        // Assert
        Assert.Contains("تم إنشاء نسخة احتياطية مجدولة بنجاح", vm.BackupStatusMessage);
        Assert.Equal(timestamp.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"), vm.LastBackupDisplayTime);
    }

    [Fact]
    public async Task AutoBackupScheduler_CatchUp_TriggersMissedBackupOnStartup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sched_catchup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var backupService = new TestBackupService { ShouldSucceed = true };
            var settingsStore = new TestBackupSettingsStore
            {
                CurrentSettings = new BackupSettings
                {
                    IsAutoBackupEnabled = true,
                    Frequency = BackupFrequency.Daily,
                    DestinationFolderPath = tempDir,
                    LastBackupUtc = DateTime.UtcNow.AddDays(-2) // Missed backup!
                }
            };

            var scheduler = new AutoBackupScheduler(
                backupService,
                settingsStore,
                NullLogger<AutoBackupScheduler>.Instance,
                checkInterval: TimeSpan.FromMinutes(10),
                startupDelay: TimeSpan.FromMilliseconds(50));

            BackupResult? completedResult = null;
            scheduler.AutoBackupCompleted += (_, res) => completedResult = res;

            // Act: start scheduler and wait for catch-up
            await scheduler.StartAsync();
            await Task.Delay(300);
            await scheduler.StopAsync();

            // Assert
            Assert.NotEmpty(backupService.CreatedBackups);
            Assert.NotNull(completedResult);
            Assert.True(completedResult!.Success);
            Assert.True(settingsStore.CurrentSettings.LastBackupUtc > DateTime.UtcNow.AddMinutes(-1));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AutoBackupScheduler_RecentBackup_DoesNotTriggerCatchUp()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sched_nocatchup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var backupService = new TestBackupService { ShouldSucceed = true };
            var settingsStore = new TestBackupSettingsStore
            {
                CurrentSettings = new BackupSettings
                {
                    IsAutoBackupEnabled = true,
                    Frequency = BackupFrequency.Daily,
                    DestinationFolderPath = tempDir,
                    LastBackupUtc = DateTime.UtcNow.AddMinutes(-30) // Recent backup, not due
                }
            };

            var scheduler = new AutoBackupScheduler(
                backupService,
                settingsStore,
                NullLogger<AutoBackupScheduler>.Instance,
                checkInterval: TimeSpan.FromMinutes(10),
                startupDelay: TimeSpan.FromMilliseconds(50));

            // Act
            await scheduler.StartAsync();
            await Task.Delay(200);
            await scheduler.StopAsync();

            // Assert: No backup should be created
            Assert.Empty(backupService.CreatedBackups);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AutoBackupScheduler_TriggerNowAsync_ExecutesBackupAndRaisesEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sched_trig_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var backupService = new TestBackupService { ShouldSucceed = true };
            var settingsStore = new TestBackupSettingsStore
            {
                CurrentSettings = new BackupSettings
                {
                    IsAutoBackupEnabled = true,
                    Frequency = BackupFrequency.Daily,
                    DestinationFolderPath = tempDir
                }
            };

            using var scheduler = new AutoBackupScheduler(
                backupService,
                settingsStore,
                NullLogger<AutoBackupScheduler>.Instance);

            BackupResult? notifiedResult = null;
            scheduler.AutoBackupCompleted += (_, res) => notifiedResult = res;

            // Act
            await scheduler.TriggerNowAsync();

            // Assert
            Assert.Single(backupService.CreatedBackups);
            Assert.NotNull(notifiedResult);
            Assert.True(notifiedResult!.Success);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DI_Container_Configuration_ResolvesSettingsViewModel_And_AutoBackupScheduler_WithStrictScopeValidation()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = "DataSource=:memory:",
                ["Application:Name"] = "Supermarket.Admin",
                ["Application:TerminalId"] = "ADMIN-PC"
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddSupermarketCore(config, "Admin");

        // Admin registrations as done in App.xaml.cs
        services.AddSingleton<IDialogService, MockDialogService>();
        services.AddSingleton<AuthSession>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<IAutoBackupScheduler, AutoBackupScheduler>();

        services.AddTransient<SettingsViewModel>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Resolve
        var scheduler = provider.GetRequiredService<IAutoBackupScheduler>();
        Assert.NotNull(scheduler);

        var vm = provider.GetRequiredService<SettingsViewModel>();
        Assert.NotNull(vm);
        Assert.NotNull(vm.BackupNowCommand);
        Assert.NotNull(vm.RestoreDatabaseCommand);
        Assert.NotNull(vm.BrowseBackupFolderCommand);
        Assert.NotNull(vm.SaveAutoBackupSettingsCommand);
    }
}

#region Test Doubles

internal sealed class TestBackupService : IBackupService
{
    public bool ShouldSucceed { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public List<string> CreatedBackups { get; } = new();
    public List<string> RestoredBackups { get; } = new();

    public Task<BackupResult> CreateBackupAsync(string destinationFilePath, CancellationToken cancellationToken = default)
    {
        if (ShouldSucceed)
        {
            CreatedBackups.Add(destinationFilePath);
            return Task.FromResult(BackupResult.Succeeded(destinationFilePath, 2048, DateTime.UtcNow));
        }

        return Task.FromResult(BackupResult.Failed(ErrorMessage ?? "Simulated backup error"));
    }

    public Task<RestoreResult> RestoreDatabaseAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        if (ShouldSucceed)
        {
            RestoredBackups.Add(backupFilePath);
            return Task.FromResult(RestoreResult.Succeeded());
        }

        return Task.FromResult(RestoreResult.Failed(ErrorMessage ?? "Simulated restore error"));
    }

    public Task<bool> CanWriteToPathAsync(string destinationPath) => Task.FromResult(true);
}

internal sealed class TestBackupSettingsStore : IBackupSettingsStore
{
    public BackupSettings CurrentSettings { get; set; } = new();

    public Task<BackupSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CurrentSettings);

    public Task SaveAsync(BackupSettings settings, CancellationToken cancellationToken = default)
    {
        CurrentSettings = settings;
        return Task.CompletedTask;
    }
}

internal sealed class TestAutoBackupScheduler : IAutoBackupScheduler
{
    public bool IsRunning { get; private set; }
    public List<BackupSettings> ReloadScheduleCalls { get; } = new();
    public event EventHandler<BackupResult>? AutoBackupCompleted;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsRunning = false;
        return Task.CompletedTask;
    }

    public Task TriggerNowAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void ReloadSchedule(BackupSettings settings) => ReloadScheduleCalls.Add(settings);

    public void RaiseCompleted(BackupResult result) => AutoBackupCompleted?.Invoke(this, result);

    public void Dispose() { }
}

#endregion
