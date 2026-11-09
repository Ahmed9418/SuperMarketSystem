using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Admin.Services;
using Supermarket.Core.Data;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Supermarket.Core.Tests;
using Xunit;

namespace Supermarket.Admin.Tests;

/// <summary>
/// Milestone 3 Tier 5 Adversarial Coverage Hardening test harness.
/// Tests probe real system implementations across Supermarket.Core and Supermarket.Admin
/// against the 4 critical stress/failure vectors:
/// 1. Rapid consecutive manual backups to the same directory / file path.
/// 2. Interrupted and aborted streams (CancellationToken cancellation and semaphore recovery).
/// 3. File system permissions, read-only attributes, and locked file handles.
/// 4. Background scheduler thread termination, rapid start/stop lifecycle, and lock contention.
/// </summary>
public sealed class Tier5AdversarialCoverageHardeningTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
            }
            catch { /* best-effort cleanup */ }
        }

        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch { /* best-effort cleanup */ }
        }
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m3_tier5_dir_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private string CreateTempFilePath(string extension = ".bak")
    {
        var dir = CreateTempDirectory();
        var file = Path.Combine(dir, $"backup_{Guid.NewGuid():N}{extension}");
        _tempFiles.Add(file);
        return file;
    }

    private (SqliteTestDatabase db, TestContextFactory factory, BackupService service) CreateHarness()
    {
        var db = new SqliteTestDatabase();
        var factory = new TestContextFactory(db);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = "Data Source=:memory:"
            })
            .Build();
        var service = new BackupService(factory, config, NullLogger<BackupService>.Instance);
        return (db, factory, service);
    }

    #region Vector 1: Rapid Consecutive Manual Backups to Same Directory

    [Fact]
    public async Task Vector1_RapidConsecutiveOverwrites_SameFilePath_SucceedsCleanlyWithoutLockingErrors()
    {
        // Stress: 10 rapid consecutive backups targeting the EXACT SAME destination file path.
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            db.Context.Categories.Add(new Category { Name = "RapidCat", Description = "Testing rapid overwrites" });
            await db.Context.SaveChangesAsync();

            var targetFile = CreateTempFilePath(".bak");

            for (int i = 0; i < 10; i++)
            {
                var result = await service.CreateBackupAsync(targetFile);

                Assert.True(result.Success, $"Iteration {i} failed: {result.ErrorMessage}");
                Assert.True(File.Exists(targetFile), $"Target file missing after iteration {i}");
                Assert.True(new FileInfo(targetFile).Length > 0, $"Target file empty after iteration {i}");
            }

            // Verify file is a valid readable SQLite database
            using var conn = new SqliteConnection($"Data Source={targetFile}");
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Categories WHERE Name = 'RapidCat';";
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task Vector1_RapidConsecutiveBackups_SameDirectoryDistinctFiles_AllFilesIntact()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var targetDir = CreateTempDirectory();
            var backupPaths = Enumerable.Range(0, 15)
                .Select(i => Path.Combine(targetDir, $"SupermarketDb_Backup_{i}_{Guid.NewGuid():N}.bak"))
                .ToList();

            foreach (var path in backupPaths)
            {
                _tempFiles.Add(path);
                var result = await service.CreateBackupAsync(path);
                Assert.True(result.Success, $"Backup failed for path {path}: {result.ErrorMessage}");
            }

            // Empirical verification: All 15 files exist and have non-zero size
            foreach (var path in backupPaths)
            {
                Assert.True(File.Exists(path));
                var info = new FileInfo(path);
                Assert.True(info.Length > 100, $"File {path} is too small ({info.Length} bytes)");
            }
        }
    }

    [Fact]
    public async Task Vector1_HighConcurrencyRace_MultipleCallersTargetingSameFile_SerializesWithoutCorruption()
    {
        // 10 concurrent tasks calling CreateBackupAsync for the exact same file path simultaneously.
        // Verifies static SemaphoreSlim _gate prevents corrupted file handles.
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            db.Context.Categories.Add(new Category { Name = "ConcurrentRacers", Description = "Race test" });
            await db.Context.SaveChangesAsync();

            var sharedTarget = CreateTempFilePath(".bak");

            var tasks = Enumerable.Range(0, 10).Select(_ => service.CreateBackupAsync(sharedTarget)).ToList();
            var results = await Task.WhenAll(tasks);

            // All should report success (serialized execution)
            foreach (var res in results)
            {
                Assert.True(res.Success, $"Concurrent backup failed: {res.ErrorMessage}");
            }

            // Verify final file is intact
            using var conn = new SqliteConnection($"Data Source={sharedTarget}");
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Categories WHERE Name = 'ConcurrentRacers';";
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(1, count);
        }
    }

    #endregion

    #region Vector 2: Interrupted / Aborted Streams & Cancellation Recovery

    [Fact]
    public async Task Vector2_PreCancelledToken_CreateBackup_ReturnsFailedAndReleasesGate()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var target = CreateTempFilePath(".bak");
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            // Act
            var cancelledResult = await service.CreateBackupAsync(target, cts.Token);

            // Empirical Assertions
            Assert.False(cancelledResult.Success);
            Assert.Contains("cancelled", cancelledResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            // Critical check: Gate must be released so subsequent backup succeeds immediately
            var subsequentResult = await service.CreateBackupAsync(target);
            Assert.True(subsequentResult.Success, "Subsequent backup deadlocked or failed after cancellation!");
            Assert.True(File.Exists(target));
        }
    }

    [Fact]
    public async Task Vector2_PreCancelledToken_RestoreDatabase_ReturnsFailedAndReleasesGate()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var target = CreateTempFilePath(".bak");
            var backupRes = await service.CreateBackupAsync(target);
            Assert.True(backupRes.Success);

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            // Act
            var restoreResult = await service.RestoreDatabaseAsync(target, cts.Token);

            // Empirical Assertions
            Assert.False(restoreResult.Success);
            Assert.Contains("cancelled", restoreResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            // Gate must be released; subsequent restore succeeds
            var subsequentRestore = await service.RestoreDatabaseAsync(target);
            Assert.True(subsequentRestore.Success, "Subsequent restore failed or deadlocked after cancellation!");
        }
    }

    [Fact]
    public async Task Vector2_InterruptedMidStream_GateIsNeverLeaked()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var target = CreateTempFilePath(".bak");

            // Cancel after 1ms (triggers during active execution or gate acquisition)
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

            try
            {
                await service.CreateBackupAsync(target, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // In case it escapes
            }

            // Prove that whether it completed or cancelled, the gate is fully usable
            var verifyTarget = CreateTempFilePath(".bak");
            var verifyResult = await service.CreateBackupAsync(verifyTarget, CancellationToken.None);
            Assert.True(verifyResult.Success, "Gate was leaked or blocked after timed cancellation!");
        }
    }

    #endregion

    #region Vector 3: File System Permissions & Read-Only / Locked Files

    [Fact]
    public async Task Vector3_DestinationFileReadOnly_ReturnsFailureGracefullyWithoutCrashing()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var target = CreateTempFilePath(".bak");
            // Create a read-only file at target path
            await File.WriteAllTextAsync(target, "read-only-content");
            File.SetAttributes(target, FileAttributes.ReadOnly);

            // Act: Attempt to backup to read-only target
            var result = await service.CreateBackupAsync(target);

            // Empirical Assertions:
            // Since File.Delete(target) on a ReadOnly file throws UnauthorizedAccessException,
            // BackupService catches the exception and returns BackupResult.Failed
            Assert.False(result.Success, "CreateBackupAsync should fail when destination file is ReadOnly.");
            Assert.NotNull(result.ErrorMessage);

            // Revert attribute for test cleanup
            File.SetAttributes(target, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task Vector3_DestinationOnInvalidDrive_ReturnsFailedGracefully()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var invalidPath = @"Z:\NonExistentDrive_X99\Backups\Supermarket.bak";

            // Act
            var result = await service.CreateBackupAsync(invalidPath);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }
    }

    [Fact]
    public async Task Vector3_RestoreLockedFile_ReturnsFailedGracefullyWithoutCrashing()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var validBackup = CreateTempFilePath(".bak");
            var backupResult = await service.CreateBackupAsync(validBackup);
            Assert.True(backupResult.Success);

            // Lock the backup file exclusively (FileShare.None)
            using var exclusiveStream = new FileStream(validBackup, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            // Act: Attempt to restore while another process holds exclusive lock
            var restoreResult = await service.RestoreDatabaseAsync(validBackup);

            // Empirical Assertions:
            Assert.False(restoreResult.Success);
            Assert.NotNull(restoreResult.ErrorMessage);
        }
    }

    [Fact]
    public async Task Vector3_CanWriteToPath_ReadOnlyDestination_EvaluatesCorrectly()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            // Non-existent drive
            Assert.False(await service.CanWriteToPathAsync(@"Z:\No_Drive_Letter\test.bak"));

            // Whitespace / null
            Assert.False(await service.CanWriteToPathAsync(null!));
            Assert.False(await service.CanWriteToPathAsync("   "));

            // Valid temp dir
            var validDir = CreateTempDirectory();
            Assert.True(await service.CanWriteToPathAsync(validDir));
        }
    }

    #endregion

    #region Vector 4: Scheduler Thread Termination, Lifecycle & Concurrency

    [Fact]
    public async Task Vector4_Scheduler_RapidStartStopCycles_DoesNotCrashOrDeadlock()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var tempDir = CreateTempDirectory();
            var settingsStore = new TestBackupSettingsStore
            {
                CurrentSettings = new BackupSettings
                {
                    IsAutoBackupEnabled = false,
                    Frequency = BackupFrequency.Daily,
                    DestinationFolderPath = tempDir
                }
            };

            using var scheduler = new AutoBackupScheduler(
                service,
                settingsStore,
                NullLogger<AutoBackupScheduler>.Instance,
                checkInterval: TimeSpan.FromSeconds(5),
                startupDelay: TimeSpan.FromMilliseconds(50));

            // Stress: 20 rapid start/stop cycles
            for (int i = 0; i < 20; i++)
            {
                await scheduler.StartAsync();
                Assert.True(scheduler.IsRunning);
                await scheduler.StopAsync();
                Assert.False(scheduler.IsRunning);
            }
        }
    }

    [Fact]
    public async Task Vector4_Scheduler_DisposeWhileRunning_TerminatesCleanly()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var tempDir = CreateTempDirectory();
            var settingsStore = new TestBackupSettingsStore
            {
                CurrentSettings = new BackupSettings
                {
                    IsAutoBackupEnabled = true,
                    Frequency = BackupFrequency.Daily,
                    DestinationFolderPath = tempDir,
                    LastBackupUtc = DateTime.UtcNow.AddHours(-30) // Overdue -> triggers immediately
                }
            };

            var scheduler = new AutoBackupScheduler(
                service,
                settingsStore,
                NullLogger<AutoBackupScheduler>.Instance,
                checkInterval: TimeSpan.FromSeconds(1),
                startupDelay: TimeSpan.FromMilliseconds(10));

            await scheduler.StartAsync();
            Assert.True(scheduler.IsRunning);

            // Abrupt Dispose() without prior StopAsync()
            scheduler.Dispose();

            // Should not throw or crash
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task Vector4_Scheduler_TriggerNowConcurrentWithAutoTick_SerializedCleanly()
    {
        var (db, factory, service) = CreateHarness();
        using (db)
        {
            var tempDir = CreateTempDirectory();
            var settingsStore = new TestBackupSettingsStore
            {
                CurrentSettings = new BackupSettings
                {
                    IsAutoBackupEnabled = true,
                    Frequency = BackupFrequency.Daily,
                    DestinationFolderPath = tempDir,
                    LastBackupUtc = null
                }
            };

            using var scheduler = new AutoBackupScheduler(
                service,
                settingsStore,
                NullLogger<AutoBackupScheduler>.Instance,
                checkInterval: TimeSpan.FromMilliseconds(100),
                startupDelay: TimeSpan.FromMilliseconds(10));

            await scheduler.StartAsync();

            // Concurrently invoke TriggerNowAsync 3 times
            var t1 = scheduler.TriggerNowAsync();
            var t2 = scheduler.TriggerNowAsync();
            var t3 = scheduler.TriggerNowAsync();

            await Task.WhenAll(t1, t2, t3);
            await scheduler.StopAsync();

            // Settings store should record last backup success
            Assert.NotNull(settingsStore.CurrentSettings.LastBackupUtc);
            Assert.Contains("تم بنجاح", settingsStore.CurrentSettings.LastBackupStatus);
        }
    }

    #endregion
}
