using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Admin.Services;
using Supermarket.Admin.ViewModels;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Admin.Tests;

/// <summary>
/// Empirical challenge and stress harness authored by Milestone 2 Challenger 2
/// to stress-test ViewModel command flows, asynchronous state transitions,
/// AutoBackupScheduler missed-backup catch-up, and JsonBackupSettingsStore resilience.
/// </summary>
public sealed class ChallengerM2StressTests : IDisposable
{
    private readonly AdminTestFixture _fixture = new();
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        _fixture.Dispose();

        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { }
            }
        }

        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m2_challenger_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private string CreateTempFilePath(string extension = ".json")
    {
        var dir = CreateTempDirectory();
        var file = Path.Combine(dir, $"settings_{Guid.NewGuid():N}{extension}");
        _tempFiles.Add(file);
        return file;
    }

    #region 1. ViewModel Command Flow: Restore Confirmation Dialog Rejection

    [Fact]
    public async Task Challenge_RestoreConfirmationDialog_RejectionAbortsCleanly_WithoutTouchingDatabaseService()
    {
        _fixture.SignInAsAdmin();
        var mockBackup = new ControllableBackupService();
        var mockSettingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            mockBackup,
            mockSettingsStore);

        // Explicitly set confirmation dialog to REJECT
        _fixture.Dialogs.ConfirmResult = false;
        bool openFileDialogInvoked = false;
        vm.OpenFileDialogOverride = () =>
        {
            openFileDialogInvoked = true;
            return @"C:\Backups\Snapshot.bak";
        };

        var initialStatus = vm.BackupStatusMessage;

        // Act: Execute RestoreDatabaseCommand
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);

        // Empirical Assertions:
        Assert.False(openFileDialogInvoked, "OpenFileDialog must NOT be opened when confirmation is rejected.");
        Assert.Equal(0, mockBackup.RestoreCallCount);
        Assert.False(vm.IsBackupBusy, "IsBackupBusy must remain false upon confirmation rejection.");
        Assert.Equal(initialStatus, vm.BackupStatusMessage);
        Assert.Empty(_fixture.Dialogs.Informations);
        Assert.Empty(_fixture.Dialogs.Warnings);
    }

    [Fact]
    public async Task Challenge_RestoreConfirmationDialog_MultipleConsecutiveRejections_RemainsStable()
    {
        _fixture.SignInAsAdmin();
        var mockBackup = new ControllableBackupService();
        var mockSettingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            mockBackup,
            mockSettingsStore);

        _fixture.Dialogs.ConfirmResult = false;

        // Stress: Execute 10 times consecutively
        for (int i = 0; i < 10; i++)
        {
            await vm.RestoreDatabaseCommand.ExecuteAsync(null);
            Assert.False(vm.IsBackupBusy);
            Assert.Equal(0, mockBackup.RestoreCallCount);
        }
    }

    #endregion

    #region 2. ViewModel Command Flow: File Dialog Cancellation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Challenge_BackupFileDialog_CancellationOrWhitespace_LeavesStateUntouched(string? cancelledPath)
    {
        _fixture.SignInAsAdmin();
        var mockBackup = new ControllableBackupService();
        var mockSettingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            mockBackup,
            mockSettingsStore);

        vm.SaveFileDialogOverride = () => cancelledPath;
        var initialStatus = vm.BackupStatusMessage;

        // Act
        await vm.BackupNowCommand.ExecuteAsync(null);

        // Empirical Assertions:
        Assert.Equal(0, mockBackup.BackupCallCount);
        Assert.False(vm.IsBackupBusy, "IsBackupBusy must remain false on dialog cancellation.");
        Assert.Equal(initialStatus, vm.BackupStatusMessage);
        Assert.Empty(_fixture.Dialogs.Informations);
        Assert.Empty(_fixture.Dialogs.Warnings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Challenge_RestoreFileDialog_CancellationOrWhitespace_LeavesStateUntouched(string? cancelledPath)
    {
        _fixture.SignInAsAdmin();
        var mockBackup = new ControllableBackupService();
        var mockSettingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            mockBackup,
            mockSettingsStore);

        _fixture.Dialogs.ConfirmResult = true; // User confirms
        vm.OpenFileDialogOverride = () => cancelledPath; // But cancels file picker
        var initialStatus = vm.BackupStatusMessage;

        // Act
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);

        // Empirical Assertions:
        Assert.Equal(0, mockBackup.RestoreCallCount);
        Assert.False(vm.IsBackupBusy, "IsBackupBusy must remain false on file picker cancellation.");
        Assert.Equal(initialStatus, vm.BackupStatusMessage);
        Assert.Empty(_fixture.Dialogs.Informations);
        Assert.Empty(_fixture.Dialogs.Warnings);
    }

    #endregion

    #region 3. Progress Bar & Asynchronous State Transitions (IsBackupBusy & Re-entrancy Protection)

    [Fact]
    public async Task Challenge_IsBackupBusy_ReflectsTrueAsyncStateTransitions_AndPreventsReentrancy()
    {
        _fixture.SignInAsAdmin();
        var mockBackup = new ControllableBackupService();
        var mockSettingsStore = new TestBackupSettingsStore();
        var scheduler = new TestAutoBackupScheduler();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            mockBackup,
            mockSettingsStore,
            scheduler);

        vm.SaveFileDialogOverride = () => @"C:\Backups\Target.bak";

        // Step 1: Baseline state
        Assert.False(vm.IsBackupBusy, "IsBackupBusy must be false before operation starts.");

        // Step 2: Configure controllable backup to delay completion via TaskCompletionSource
        var tcs = new TaskCompletionSource<BackupResult>();
        mockBackup.CreateBackupTaskFactory = () => tcs.Task;

        // Start BackupNowCommand asynchronously
        var executionTask = vm.BackupNowCommand.ExecuteAsync(null);

        // Yield execution to allow BackupNowCommand to start and hit the await point
        await Task.Yield();
        await Task.Delay(50);

        // Step 3: Verify IsBackupBusy is true during active execution
        Assert.True(vm.IsBackupBusy, "IsBackupBusy must be true while backup is in flight.");
        Assert.Contains("جاري إنشاء النسخة", vm.BackupStatusMessage);

        // Step 4: Re-entrancy attack: Attempt concurrent invocations while IsBackupBusy is true
        // 4a. BackupNowCommand re-entrant attempt
        await vm.BackupNowCommand.ExecuteAsync(null);
        Assert.Equal(1, mockBackup.BackupCallCount); // Call count MUST NOT increment

        // 4b. RestoreDatabaseCommand concurrent attempt
        _fixture.Dialogs.ConfirmResult = true;
        vm.OpenFileDialogOverride = () => @"C:\Backups\Another.bak";
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);
        Assert.Equal(0, mockBackup.RestoreCallCount); // Must be rejected immediately

        // 4c. SaveAutoBackupSettingsCommand concurrent attempt
        vm.IsAutoBackupEnabled = true;
        vm.AutoBackupFolderPath = @"C:\Backups";
        await vm.SaveAutoBackupSettingsCommand.ExecuteAsync(null);
        Assert.Empty(scheduler.ReloadScheduleCalls); // Must be rejected immediately

        // Step 5: Complete the active backup operation
        tcs.SetResult(BackupResult.Succeeded(@"C:\Backups\Target.bak", 4096, DateTime.UtcNow));
        await executionTask;

        // Step 6: Verify IsBackupBusy transitions back to false
        Assert.False(vm.IsBackupBusy, "IsBackupBusy must transition back to false when operation finishes.");
        Assert.Contains("تم إنشاء النسخة الاحتياطية بنجاح", vm.BackupStatusMessage);
    }

    [Fact]
    public async Task Challenge_IsBackupBusy_TransitionsBackToFalse_EvenWhenExceptionThrown()
    {
        _fixture.SignInAsAdmin();
        var mockBackup = new ControllableBackupService
        {
            CreateBackupTaskFactory = () => throw new IOException("Disk failure simulated")
        };
        var mockSettingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            mockBackup,
            mockSettingsStore);

        vm.SaveFileDialogOverride = () => @"C:\Backups\Throws.bak";

        // Act
        await vm.BackupNowCommand.ExecuteAsync(null);

        // Empirical Assertions:
        Assert.False(vm.IsBackupBusy, "IsBackupBusy must be reset to false in finally block even on fatal exception.");
        Assert.Contains("خطأ: Disk failure simulated", vm.BackupStatusMessage);
        Assert.Single(_fixture.Dialogs.Warnings);
    }

    [Fact]
    public async Task Challenge_IsBackupBusy_RestoreException_TransitionsBackToFalse()
    {
        _fixture.SignInAsAdmin();
        var mockBackup = new ControllableBackupService
        {
            RestoreDatabaseTaskFactory = () => throw new InvalidOperationException("Exclusive access lock failed")
        };
        var mockSettingsStore = new TestBackupSettingsStore();

        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session,
            mockBackup,
            mockSettingsStore);

        _fixture.Dialogs.ConfirmResult = true;
        vm.OpenFileDialogOverride = () => @"C:\Backups\Snapshot.bak";

        // Act
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);

        // Empirical Assertions:
        Assert.False(vm.IsBackupBusy, "IsBackupBusy must be reset to false on restore exception.");
        Assert.Contains("خطأ: Exclusive access lock failed", vm.BackupStatusMessage);
        Assert.Single(_fixture.Dialogs.Warnings);
    }

    #endregion

    #region 4. AutoBackupScheduler: Missed-Backup Catch-Up Stress

    [Fact]
    public async Task Challenge_AutoBackupScheduler_CatchUp_DailyOverdue_TriggersImmediately()
    {
        var tempDir = CreateTempDirectory();
        var mockBackup = new ControllableBackupService();
        var settingsStore = new TestBackupSettingsStore
        {
            CurrentSettings = new BackupSettings
            {
                IsAutoBackupEnabled = true,
                Frequency = BackupFrequency.Daily,
                DestinationFolderPath = tempDir,
                LastBackupUtc = DateTime.UtcNow.AddHours(-25) // 25 hours ago -> overdue for daily (24h)
            }
        };

        using var scheduler = new AutoBackupScheduler(
            mockBackup,
            settingsStore,
            NullLogger<AutoBackupScheduler>.Instance,
            checkInterval: TimeSpan.FromMinutes(10),
            startupDelay: TimeSpan.FromMilliseconds(50));

        BackupResult? completedResult = null;
        scheduler.AutoBackupCompleted += (_, res) => completedResult = res;

        // Act: Start scheduler
        await scheduler.StartAsync();
        await Task.Delay(250);
        await scheduler.StopAsync();

        // Assert: Missed backup was caught up
        Assert.Equal(1, mockBackup.BackupCallCount);
        Assert.NotNull(completedResult);
        Assert.True(completedResult!.Success);
        Assert.True(settingsStore.CurrentSettings.LastBackupUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Challenge_AutoBackupScheduler_CatchUp_WeeklyOverdue_TriggersImmediately()
    {
        var tempDir = CreateTempDirectory();
        var mockBackup = new ControllableBackupService();
        var settingsStore = new TestBackupSettingsStore
        {
            CurrentSettings = new BackupSettings
            {
                IsAutoBackupEnabled = true,
                Frequency = BackupFrequency.Weekly,
                DestinationFolderPath = tempDir,
                LastBackupUtc = DateTime.UtcNow.AddDays(-8) // 8 days ago -> overdue for weekly (7d)
            }
        };

        using var scheduler = new AutoBackupScheduler(
            mockBackup,
            settingsStore,
            NullLogger<AutoBackupScheduler>.Instance,
            checkInterval: TimeSpan.FromMinutes(10),
            startupDelay: TimeSpan.FromMilliseconds(50));

        // Act
        await scheduler.StartAsync();
        await Task.Delay(250);
        await scheduler.StopAsync();

        // Assert: Missed backup was caught up
        Assert.Equal(1, mockBackup.BackupCallCount);
        Assert.True(settingsStore.CurrentSettings.LastBackupUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Challenge_AutoBackupScheduler_CatchUp_NeverBackedUpBefore_TriggersImmediately()
    {
        var tempDir = CreateTempDirectory();
        var mockBackup = new ControllableBackupService();
        var settingsStore = new TestBackupSettingsStore
        {
            CurrentSettings = new BackupSettings
            {
                IsAutoBackupEnabled = true,
                Frequency = BackupFrequency.Daily,
                DestinationFolderPath = tempDir,
                LastBackupUtc = null // Never backed up before!
            }
        };

        using var scheduler = new AutoBackupScheduler(
            mockBackup,
            settingsStore,
            NullLogger<AutoBackupScheduler>.Instance,
            checkInterval: TimeSpan.FromMinutes(10),
            startupDelay: TimeSpan.FromMilliseconds(50));

        // Act
        await scheduler.StartAsync();
        await Task.Delay(250);
        await scheduler.StopAsync();

        // Assert: Catch-up backup must trigger immediately because LastBackupUtc is null
        Assert.Equal(1, mockBackup.BackupCallCount);
        Assert.NotNull(settingsStore.CurrentSettings.LastBackupUtc);
    }

    [Fact]
    public async Task Challenge_AutoBackupScheduler_CatchUp_Disabled_DoesNotTrigger()
    {
        var tempDir = CreateTempDirectory();
        var mockBackup = new ControllableBackupService();
        var settingsStore = new TestBackupSettingsStore
        {
            CurrentSettings = new BackupSettings
            {
                IsAutoBackupEnabled = false, // Disabled!
                Frequency = BackupFrequency.Daily,
                DestinationFolderPath = tempDir,
                LastBackupUtc = DateTime.UtcNow.AddDays(-30) // Very old
            }
        };

        using var scheduler = new AutoBackupScheduler(
            mockBackup,
            settingsStore,
            NullLogger<AutoBackupScheduler>.Instance,
            checkInterval: TimeSpan.FromMinutes(10),
            startupDelay: TimeSpan.FromMilliseconds(50));

        // Act
        await scheduler.StartAsync();
        await Task.Delay(200);
        await scheduler.StopAsync();

        // Assert: Disabled scheduler must not trigger backup
        Assert.Equal(0, mockBackup.BackupCallCount);
    }

    [Fact]
    public async Task Challenge_AutoBackupScheduler_CatchUp_AutoCreatesMissingDestinationDirectory()
    {
        var baseDir = CreateTempDirectory();
        var nonExistentSubDir = Path.Combine(baseDir, "ScheduledSubDir", "2026");
        _tempDirectories.Add(nonExistentSubDir);

        var mockBackup = new ControllableBackupService();
        var settingsStore = new TestBackupSettingsStore
        {
            CurrentSettings = new BackupSettings
            {
                IsAutoBackupEnabled = true,
                Frequency = BackupFrequency.Daily,
                DestinationFolderPath = nonExistentSubDir, // Directory doesn't exist yet
                LastBackupUtc = DateTime.UtcNow.AddHours(-30)
            }
        };

        using var scheduler = new AutoBackupScheduler(
            mockBackup,
            settingsStore,
            NullLogger<AutoBackupScheduler>.Instance,
            checkInterval: TimeSpan.FromMinutes(10),
            startupDelay: TimeSpan.FromMilliseconds(50));

        // Act
        await scheduler.StartAsync();
        await Task.Delay(250);
        await scheduler.StopAsync();

        // Assert: Directory was created and backup executed
        Assert.True(Directory.Exists(nonExistentSubDir), "AutoBackupScheduler must auto-create missing destination directory.");
        Assert.Equal(1, mockBackup.BackupCallCount);
    }

    [Fact]
    public async Task Challenge_AutoBackupScheduler_EmptyDestinationPath_LogsWarningAndDoesNotCrash()
    {
        var mockBackup = new ControllableBackupService();
        var settingsStore = new TestBackupSettingsStore
        {
            CurrentSettings = new BackupSettings
            {
                IsAutoBackupEnabled = true,
                DestinationFolderPath = "", // Empty
                LastBackupUtc = DateTime.UtcNow.AddHours(-30)
            }
        };

        using var scheduler = new AutoBackupScheduler(
            mockBackup,
            settingsStore,
            NullLogger<AutoBackupScheduler>.Instance,
            checkInterval: TimeSpan.FromMinutes(10),
            startupDelay: TimeSpan.FromMilliseconds(50));

        // Act: Should not throw
        await scheduler.StartAsync();
        await Task.Delay(150);
        await scheduler.StopAsync();

        Assert.Equal(0, mockBackup.BackupCallCount);
    }

    #endregion

    #region 5. Local JSON Settings Persistence & Restart Survival (Decoupled from Database)

    [Fact]
    public async Task Challenge_JsonBackupSettingsStore_PersistsAndAccuratelyReloadsAcrossRestarts_WithoutDatabase()
    {
        var settingsFilePath = CreateTempFilePath(".json");

        // 1. Session 1: Create store, configure, and save settings
        var timestamp = new DateTime(2026, 11, 9, 14, 45, 0, DateTimeKind.Utc);
        var initialSettings = new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Weekly,
            DestinationFolderPath = @"D:\Supermarket_Scheduled_Backups",
            LastBackupUtc = timestamp,
            LastBackupStatus = "تم إنشاء النسخة التلقائية بنجاح"
        };

        var storeSession1 = new JsonBackupSettingsStore(settingsFilePath);
        await storeSession1.SaveAsync(initialSettings);

        // Assert file exists on disk and is non-empty
        Assert.True(File.Exists(settingsFilePath));
        Assert.True(new FileInfo(settingsFilePath).Length > 0);

        // 2. Simulate "Application Restart": instantiate brand new store instance
        // Notice: NO database connection string, NO EF Core context, 100% decoupled
        var storeSession2 = new JsonBackupSettingsStore(settingsFilePath);
        var loadedSettings = await storeSession2.LoadAsync();

        // Empirical Assertions:
        Assert.NotNull(loadedSettings);
        Assert.True(loadedSettings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Weekly, loadedSettings.Frequency);
        Assert.Equal(@"D:\Supermarket_Scheduled_Backups", loadedSettings.DestinationFolderPath);
        Assert.Equal(timestamp, loadedSettings.LastBackupUtc);
        Assert.Equal("تم إنشاء النسخة التلقائية بنجاح", loadedSettings.LastBackupStatus);
    }

    [Fact]
    public async Task Challenge_JsonBackupSettingsStore_CorruptedJsonFile_GracefullyFallsBackToDefaults()
    {
        var settingsFilePath = CreateTempFilePath(".json");
        await File.WriteAllTextAsync(settingsFilePath, "{ INVALID_CORRUPTED_JSON_CONTENT [1, 2, 3] }}}");

        var store = new JsonBackupSettingsStore(settingsFilePath);

        // Act: Must not throw unhandled exception
        var settings = await store.LoadAsync();

        // Assert: Defaults returned
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Daily, settings.Frequency);
        Assert.Null(settings.DestinationFolderPath);
        Assert.Null(settings.LastBackupUtc);
    }

    [Fact]
    public async Task Challenge_JsonBackupSettingsStore_NonExistentFile_ReturnsDefaultsWithoutError()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"no_file_{Guid.NewGuid():N}.json");

        var store = new JsonBackupSettingsStore(nonExistentPath);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled);
    }

    [Fact]
    public async Task Challenge_JsonBackupSettingsStore_ConcurrentReadWriteStress_MaintainsFileIntegrity()
    {
        var settingsFilePath = CreateTempFilePath(".json");
        var store = new JsonBackupSettingsStore(settingsFilePath);

        // Act: 20 concurrent tasks performing mixed Read and Save operations
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            var iteration = i;
            if (iteration % 2 == 0)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await store.SaveAsync(new BackupSettings
                    {
                        IsAutoBackupEnabled = true,
                        Frequency = iteration % 4 == 0 ? BackupFrequency.Weekly : BackupFrequency.Daily,
                        DestinationFolderPath = $@"C:\Backups\Iter_{iteration}",
                        LastBackupUtc = DateTime.UtcNow
                    });
                }));
            }
            else
            {
                tasks.Add(Task.Run(async () =>
                {
                    var s = await store.LoadAsync();
                    Assert.NotNull(s);
                }));
            }
        }

        await Task.WhenAll(tasks);

        // Final Assert: The file remains valid, non-corrupt JSON
        var finalSettings = await store.LoadAsync();
        Assert.NotNull(finalSettings);
        Assert.True(File.Exists(settingsFilePath));

        var rawContent = await File.ReadAllTextAsync(settingsFilePath);
        using var jsonDoc = JsonDocument.Parse(rawContent);
        Assert.NotNull(jsonDoc);
    }

    #endregion

    #region 6. Null Service & Corner Case Defenses

    [Fact]
    public async Task Challenge_ViewModel_NullBackupService_ShowsWarningAndDoesNotCrash()
    {
        _fixture.SignInAsAdmin();
        // Construct ViewModel with null backup service (simulating service unavailability)
        var vm = new SettingsViewModel(
            _fixture.StoreSettings,
            _fixture.Reports,
            _fixture.Dialogs,
            _fixture.Session);

        // Act 1: BackupNowAsync with null service
        await vm.BackupNowCommand.ExecuteAsync(null);
        Assert.Single(_fixture.Dialogs.Warnings);
        Assert.Contains("غير متوفرة", _fixture.Dialogs.Warnings[0]);

        // Act 2: RestoreDatabaseAsync with null service
        await vm.RestoreDatabaseCommand.ExecuteAsync(null);
        Assert.Equal(2, _fixture.Dialogs.Warnings.Count);
        Assert.Contains("غير متوفرة", _fixture.Dialogs.Warnings[1]);

        // Act 3: SaveAutoBackupSettingsAsync with null store
        await vm.SaveAutoBackupSettingsCommand.ExecuteAsync(null);
        Assert.Equal(3, _fixture.Dialogs.Warnings.Count);
    }

    #endregion
}

#region Controllable Test Double for Asynchronous Stress Testing

internal sealed class ControllableBackupService : IBackupService
{
    public int BackupCallCount { get; private set; }
    public int RestoreCallCount { get; private set; }

    public Func<Task<BackupResult>>? CreateBackupTaskFactory { get; set; }
    public Func<Task<RestoreResult>>? RestoreDatabaseTaskFactory { get; set; }

    public async Task<BackupResult> CreateBackupAsync(string destinationFilePath, CancellationToken cancellationToken = default)
    {
        BackupCallCount++;
        if (CreateBackupTaskFactory != null)
        {
            return await CreateBackupTaskFactory();
        }

        return BackupResult.Succeeded(destinationFilePath, 1024, DateTime.UtcNow);
    }

    public async Task<RestoreResult> RestoreDatabaseAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        RestoreCallCount++;
        if (RestoreDatabaseTaskFactory != null)
        {
            return await RestoreDatabaseTaskFactory();
        }

        return RestoreResult.Succeeded();
    }

    public Task<bool> CanWriteToPathAsync(string destinationPath) => Task.FromResult(true);
}

#endregion
