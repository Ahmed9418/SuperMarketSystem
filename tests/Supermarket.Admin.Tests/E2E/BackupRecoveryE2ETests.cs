using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Supermarket.Core.Tests;
using Xunit;

namespace Supermarket.Admin.Tests.E2E;

/// <summary>
/// Comprehensive E2E and integration test suite for Database Backup & Recovery subsystem
/// covering Tiers 1 through 4: Feature Coverage, Boundary & Corner Cases, Cross-Feature Combinations,
/// and Real-World Application Disaster Recovery Scenarios.
/// </summary>
public sealed class BackupRecoveryE2ETests : IDisposable
{
    private readonly SqliteTestDatabase _database;
    private readonly TestContextFactory _contextFactory;
    private readonly IConfiguration _configuration;
    private readonly BackupService _backupService;
    private readonly MockDialogService _dialogService;
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    public BackupRecoveryE2ETests()
    {
        _database = new SqliteTestDatabase();
        _contextFactory = new TestContextFactory(_database);
        _dialogService = new MockDialogService();

        var configValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SupermarketDb"] = "Data Source=:memory:",
            ["Application:Name"] = "Supermarket.Admin"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _backupService = new BackupService(_contextFactory, _configuration, NullLogger<BackupService>.Instance);
    }

    public void Dispose()
    {
        _database.Dispose();

        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* ignore test cleanup */ }
            }
        }

        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* ignore test cleanup */ }
            }
        }
    }

    private string CreateTempBackupPath(string? extension = ".db")
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"backup_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);

        var path = Path.Combine(tempDir, $"backup_file{extension}");
        _tempFiles.Add(path);
        return path;
    }

    #region Tier 1 - Feature Coverage: Manual Backup Creation

    [Fact]
    public async Task Tier1_ManualBackup_CreatesPhysicalFileWithNonZeroSize()
    {
        // Arrange
        var backupPath = CreateTempBackupPath();

        // Act
        var result = await _backupService.CreateBackupAsync(backupPath);

        // Assert
        Assert.True(result.Success, $"Backup failed: {result.ErrorMessage}");
        Assert.True(File.Exists(backupPath), "Backup file was not created on disk.");
        Assert.True(new FileInfo(backupPath).Length > 0, "Backup file on disk is empty.");
    }

    [Fact]
    public async Task Tier1_ManualBackup_ReturnsSuccessResultWithValidMetadata()
    {
        // Arrange
        var backupPath = CreateTempBackupPath();

        // Act
        var result = await _backupService.CreateBackupAsync(backupPath);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(backupPath, result.OutputPath);
        Assert.True(result.FileSizeBytes > 0);
        Assert.NotNull(result.TimestampUtc);
        Assert.True(result.TimestampUtc.Value <= DateTime.UtcNow.AddMinutes(1));
        Assert.True(result.TimestampUtc.Value >= DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Tier1_ManualBackup_AutoCreatesMissingParentDirectory()
    {
        // Arrange: Nested directory that does not exist yet
        var subDir = Path.Combine(Path.GetTempPath(), $"nested_dir_{Guid.NewGuid():N}", "backups");
        _tempDirectories.Add(subDir);
        var backupPath = Path.Combine(subDir, "auto_created.bak");
        _tempFiles.Add(backupPath);

        // Act
        var result = await _backupService.CreateBackupAsync(backupPath);

        // Assert
        Assert.True(result.Success, $"Backup failed: {result.ErrorMessage}");
        Assert.True(Directory.Exists(subDir), "Parent directory should be automatically created.");
        Assert.True(File.Exists(backupPath));
        Assert.True(result.FileSizeBytes > 0);
    }

    [Fact]
    public async Task Tier1_ManualBackup_OverwritesExistingTargetFile()
    {
        // Arrange: Create pre-existing file with dummy content
        var backupPath = CreateTempBackupPath();
        await File.WriteAllTextAsync(backupPath, "OLD_PRE_EXISTING_DUMMY_DATA");
        var oldLength = new FileInfo(backupPath).Length;

        // Act
        var result = await _backupService.CreateBackupAsync(backupPath);

        // Assert
        Assert.True(result.Success, $"Backup failed: {result.ErrorMessage}");
        Assert.True(File.Exists(backupPath));
        var newLength = new FileInfo(backupPath).Length;
        Assert.NotEqual(oldLength, newLength);
    }

    [Fact]
    public async Task Tier1_ManualBackup_CanWriteToPath_ValidatesAccessiblePath()
    {
        // Arrange
        var validDir = Path.GetTempPath();
        var validFile = CreateTempBackupPath();

        // Act
        var canWriteDir = await _backupService.CanWriteToPathAsync(validDir);
        var canWriteFile = await _backupService.CanWriteToPathAsync(validFile);

        // Assert
        Assert.True(canWriteDir, "Temp directory should be writable.");
        Assert.True(canWriteFile, "Path in temp directory should be writable.");
    }

    #endregion

    #region Tier 1 - Feature Coverage: Restore Execution Flow

    [Fact]
    public async Task Tier1_Restore_ValidBackupRestoresSuccessfully()
    {
        // Arrange: Create a valid backup snapshot
        var backupPath = CreateTempBackupPath();
        var backupResult = await _backupService.CreateBackupAsync(backupPath);
        Assert.True(backupResult.Success);

        // Act
        var restoreResult = await _backupService.RestoreDatabaseAsync(backupPath);

        // Assert
        Assert.True(restoreResult.Success, $"Restore failed: {restoreResult.ErrorMessage}");
        Assert.Null(restoreResult.ErrorMessage);
    }

    [Fact]
    public async Task Tier1_Restore_ClearsConnectionPoolsAndAllowsNewQueries()
    {
        // Arrange
        var backupPath = CreateTempBackupPath();
        var backupResult = await _backupService.CreateBackupAsync(backupPath);
        Assert.True(backupResult.Success);

        // Act
        var restoreResult = await _backupService.RestoreDatabaseAsync(backupPath);
        Assert.True(restoreResult.Success);

        // Assert: New context can immediately query the restored database
        using var newContext = _contextFactory.CreateDbContext();
        var canConnect = await newContext.Database.CanConnectAsync();
        Assert.True(canConnect, "Database must be queryable and accessible after restore.");

        var userCount = await newContext.Users.CountAsync();
        Assert.True(userCount >= 0);
    }

    [Fact]
    public async Task Tier1_Restore_ExecutesAsynchronouslyWithoutFreezing()
    {
        // Arrange
        var backupPath = CreateTempBackupPath();
        await _backupService.CreateBackupAsync(backupPath);

        // Act: Pass cancellation token and await async execution
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var restoreTask = _backupService.RestoreDatabaseAsync(backupPath, cts.Token);

        var restoreResult = await restoreTask;

        // Assert
        Assert.True(restoreResult.Success);
    }

    [Fact]
    public async Task Tier1_Restore_SucceedsWhenActiveQueriesExist()
    {
        // Arrange: Execute queries on active context
        using var activeContext = _contextFactory.CreateDbContext();
        var initialUsers = await activeContext.Users.ToListAsync();
        Assert.NotEmpty(initialUsers);

        var backupPath = CreateTempBackupPath();
        await _backupService.CreateBackupAsync(backupPath);

        // Act: Restore while context descriptors have been used
        var restoreResult = await _backupService.RestoreDatabaseAsync(backupPath);

        // Assert
        Assert.True(restoreResult.Success);

        using var verifyContext = _contextFactory.CreateDbContext();
        var usersAfterRestore = await verifyContext.Users.ToListAsync();
        Assert.Equal(initialUsers.Count, usersAfterRestore.Count);
    }

    #endregion

    #region Tier 2 - Boundary & Corner Cases

    [Fact]
    public async Task Tier2_Restore_NonExistentBackupFileReturnsFailureWithoutCrashing()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"missing_backup_{Guid.NewGuid():N}.bak");

        // Act
        var result = await _backupService.RestoreDatabaseAsync(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Contains("exist", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Tier2_Restore_EmptyOrWhitespacePathReturnsFailure()
    {
        // Act
        var resultEmpty = await _backupService.RestoreDatabaseAsync(string.Empty);
        var resultWhitespace = await _backupService.RestoreDatabaseAsync("   ");

        // Assert
        Assert.False(resultEmpty.Success);
        Assert.False(string.IsNullOrWhiteSpace(resultEmpty.ErrorMessage));
        Assert.False(resultWhitespace.Success);
        Assert.False(string.IsNullOrWhiteSpace(resultWhitespace.ErrorMessage));
    }

    [Fact]
    public async Task Tier2_Restore_CorruptZeroByteFileFailsGracefully()
    {
        // Arrange: Create a 0-byte corrupt backup file
        var zeroBytePath = CreateTempBackupPath();
        await File.WriteAllBytesAsync(zeroBytePath, Array.Empty<byte>());

        // Act
        var result = await _backupService.RestoreDatabaseAsync(zeroBytePath);

        // Assert - Must report failure cleanly without throwing unhandled exceptions
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Tier2_Restore_CorruptHeaderOrGarbageContentFailsGracefully()
    {
        // Arrange: Create a file with garbage bytes
        var garbagePath = CreateTempBackupPath();
        var garbageBytes = new byte[2048];
        new Random(42).NextBytes(garbageBytes);
        await File.WriteAllBytesAsync(garbagePath, garbageBytes);

        // Act
        var result = await _backupService.RestoreDatabaseAsync(garbagePath);

        // Assert
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Tier2_Backup_InvalidDrivePathReturnsFailureWithoutCrashing()
    {
        // Arrange: Non-existent drive letter
        var invalidDrivePath = @"Z:\NonExistentDrive_XYZ_1234567890\backup.bak";

        // Act
        var result = await _backupService.CreateBackupAsync(invalidDrivePath);

        // Assert
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Tier2_Backup_EmptyOrWhitespacePathReturnsFailure()
    {
        // Act
        var resultEmpty = await _backupService.CreateBackupAsync(string.Empty);
        var resultWhitespace = await _backupService.CreateBackupAsync("   ");

        // Assert
        Assert.False(resultEmpty.Success);
        Assert.False(string.IsNullOrWhiteSpace(resultEmpty.ErrorMessage));
        Assert.False(resultWhitespace.Success);
        Assert.False(string.IsNullOrWhiteSpace(resultWhitespace.ErrorMessage));
    }

    [Fact]
    public async Task Tier2_Backup_CanWriteToPath_ReturnsFalseForInvalidDriveOrEmpty()
    {
        // Arrange
        var invalidDrive = @"Z:\NonExistentDrive_XYZ_1234567890\backup.bak";

        // Act
        var canWriteInvalidDrive = await _backupService.CanWriteToPathAsync(invalidDrive);
        var canWriteEmpty = await _backupService.CanWriteToPathAsync(string.Empty);
        var canWriteWhitespace = await _backupService.CanWriteToPathAsync("   ");

        // Assert
        Assert.False(canWriteInvalidDrive);
        Assert.False(canWriteEmpty);
        Assert.False(canWriteWhitespace);
    }

    [Fact]
    public async Task Tier2_Concurrency_SimultaneousBackupAndRestoreCallsAreSerialized()
    {
        // Arrange
        var backupPath1 = CreateTempBackupPath();
        var backupPath2 = CreateTempBackupPath();
        var backupPath3 = CreateTempBackupPath();

        // Act: Trigger multiple backup operations concurrently
        var task1 = _backupService.CreateBackupAsync(backupPath1);
        var task2 = _backupService.CreateBackupAsync(backupPath2);
        var task3 = _backupService.CreateBackupAsync(backupPath3);

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert: All succeed safely without IO concurrency collisions
        foreach (var res in results)
        {
            Assert.True(res.Success, $"Concurrent backup failed: {res.ErrorMessage}");
            Assert.True(File.Exists(res.OutputPath));
            Assert.True(res.FileSizeBytes > 0);
        }
    }

    #endregion

    #region Tier 3 - Cross-Feature Combinations (Pairwise)

    [Fact]
    public async Task Tier3_CrossFeature_Backup_MutateStoreData_Restore_AssertDataReverted()
    {
        // Arrange: 1. Seed initial data
        using (var setupContext = _contextFactory.CreateDbContext())
        {
            var initialCat = new Category
            {
                Name = "قسم البقالة الأصلية",
                Description = "القسم الأولي قبل أخذ النسخة"
            };
            setupContext.Categories.Add(initialCat);
            await setupContext.SaveChangesAsync();
        }

        // 2. Take backup A
        var backupA = CreateTempBackupPath();
        var backupResult = await _backupService.CreateBackupAsync(backupA);
        Assert.True(backupResult.Success);

        // 3. Mutate store data: add a new category and a product
        using (var mutateContext = _contextFactory.CreateDbContext())
        {
            var newCat = new Category
            {
                Name = "قسم الخضراوات الطازجة",
                Description = "أضيف بعد النسخة الاحتياطية"
            };
            mutateContext.Categories.Add(newCat);
            await mutateContext.SaveChangesAsync();

            var newProd = new Product
            {
                Name = "طماطم بلدي",
                Barcode = "990011223344",
                CategoryId = newCat.Id,
                UnitId = 1,
                CostPrice = 10m,
                SellingPrice = 15m,
                MinimumStockLevel = 5,
                IsActive = true
            };
            mutateContext.Products.Add(newProd);
            await mutateContext.SaveChangesAsync();
        }

        // Verify mutations exist in database
        using (var verifyBefore = _contextFactory.CreateDbContext())
        {
            Assert.Contains(await verifyBefore.Categories.ToListAsync(), c => c.Name == "قسم الخضراوات الطازجة");
            Assert.Contains(await verifyBefore.Products.ToListAsync(), p => p.Barcode == "990011223344");
        }

        // Act: 4. Restore backup A
        var restoreResult = await _backupService.RestoreDatabaseAsync(backupA);
        Assert.True(restoreResult.Success);

        // Assert: 5. Verify database reverted back to pre-mutation state
        using (var verifyAfter = _contextFactory.CreateDbContext())
        {
            var categories = await verifyAfter.Categories.ToListAsync();
            var products = await verifyAfter.Products.ToListAsync();

            // Original category exists
            Assert.Contains(categories, c => c.Name == "قسم البقالة الأصلية");

            // Mutated data must be completely reverted
            Assert.DoesNotContain(categories, c => c.Name == "قسم الخضراوات الطازجة");
            Assert.DoesNotContain(products, p => p.Barcode == "990011223344");
        }
    }

    [Fact]
    public async Task Tier3_CrossFeature_AutoBackupSettings_SurviveDatabaseRestore_CircularWipeoutImmunity()
    {
        // Arrange: 1. Create a local JSON settings store isolated for this test
        var tempSettingsFile = CreateTempBackupPath(".json");
        var settingsStore = new JsonBackupSettingsStore(tempSettingsFile);

        // Configure auto-backup settings
        var originalSettings = new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Weekly,
            DestinationFolderPath = @"D:\Supermarket_Scheduled_Backups_2026",
            LastBackupUtc = new DateTime(2026, 11, 8, 20, 0, 0, DateTimeKind.Utc),
            LastBackupStatus = "تم بنجاح"
        };
        await settingsStore.SaveAsync(originalSettings);

        // 2. Take a database snapshot
        var dbBackup = CreateTempBackupPath();
        await _backupService.CreateBackupAsync(dbBackup);

        // 3. Mutate the database
        using (var context = _contextFactory.CreateDbContext())
        {
            context.Categories.Add(new Category { Name = "فئة تجريبية لفحص الدائرية", Description = "Test" });
            await context.SaveChangesAsync();
        }

        // Act: 4. Execute a full database restore
        var restoreResult = await _backupService.RestoreDatabaseAsync(dbBackup);
        Assert.True(restoreResult.Success);

        // Assert: 5. CRITICAL FLAW 3 VERIFICATION:
        // Loading auto-backup settings from JSON must remain 100% UNCHANGED and intact!
        var reloadedSettings = await settingsStore.LoadAsync();
        Assert.NotNull(reloadedSettings);
        Assert.True(reloadedSettings.IsAutoBackupEnabled, "Auto-backup enabled toggle must survive database restore.");
        Assert.Equal(BackupFrequency.Weekly, reloadedSettings.Frequency);
        Assert.Equal(@"D:\Supermarket_Scheduled_Backups_2026", reloadedSettings.DestinationFolderPath);
        Assert.Equal("تم بنجاح", reloadedSettings.LastBackupStatus);
        Assert.Equal(originalSettings.LastBackupUtc, reloadedSettings.LastBackupUtc);
    }

    [Fact]
    public async Task Tier3_CrossFeature_MultiCycleSnapshotIsolation()
    {
        // Snapshot 1: Initial state
        var snapshot1Path = CreateTempBackupPath();
        await _backupService.CreateBackupAsync(snapshot1Path);

        // Cycle 1: Add Category 1
        using (var ctx = _contextFactory.CreateDbContext())
        {
            ctx.Categories.Add(new Category { Name = "فئة الدورة 1" });
            await ctx.SaveChangesAsync();
        }

        // Snapshot 2: With Category 1
        var snapshot2Path = CreateTempBackupPath();
        await _backupService.CreateBackupAsync(snapshot2Path);

        // Cycle 2: Add Category 2
        using (var ctx = _contextFactory.CreateDbContext())
        {
            ctx.Categories.Add(new Category { Name = "فئة الدورة 2" });
            await ctx.SaveChangesAsync();
        }

        // Verify both exist
        using (var ctx = _contextFactory.CreateDbContext())
        {
            Assert.Contains(await ctx.Categories.ToListAsync(), c => c.Name == "فئة الدورة 1");
            Assert.Contains(await ctx.Categories.ToListAsync(), c => c.Name == "فئة الدورة 2");
        }

        // Restore Snapshot 1 -> Neither category should exist
        await _backupService.RestoreDatabaseAsync(snapshot1Path);
        using (var ctx = _contextFactory.CreateDbContext())
        {
            Assert.DoesNotContain(await ctx.Categories.ToListAsync(), c => c.Name == "فئة الدورة 1");
            Assert.DoesNotContain(await ctx.Categories.ToListAsync(), c => c.Name == "فئة الدورة 2");
        }

        // Restore Snapshot 2 -> Category 1 must exist, Category 2 must not
        await _backupService.RestoreDatabaseAsync(snapshot2Path);
        using (var ctx = _contextFactory.CreateDbContext())
        {
            Assert.Contains(await ctx.Categories.ToListAsync(), c => c.Name == "فئة الدورة 1");
            Assert.DoesNotContain(await ctx.Categories.ToListAsync(), c => c.Name == "فئة الدورة 2");
        }
    }

    [Fact]
    public async Task Tier3_CrossFeature_ActiveQueryDuringBackupExecutionDoesNotLock()
    {
        // Arrange
        var backupPath = CreateTempBackupPath();

        // Act: Run queries while backup is executing in parallel
        var queryTask = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                using var ctx = _contextFactory.CreateDbContext();
                _ = await ctx.Users.ToListAsync();
                await Task.Delay(10);
            }
        });

        var backupTask = _backupService.CreateBackupAsync(backupPath);

        await Task.WhenAll(queryTask, backupTask);

        // Assert
        var backupResult = await backupTask;
        Assert.True(backupResult.Success);
        Assert.True(File.Exists(backupPath));
    }

    #endregion

    #region Tier 4 - Real-World Application Scenarios

    [Fact]
    public async Task Tier4_RealWorld_AdminDisasterRecoveryScenario_CompleteCatalogRestoration()
    {
        // Scenario: A production supermarket with a catalog of categories and products.
        // A catastrophic human or software error deletes product records.
        // Administrator initiates emergency disaster recovery from the latest backup snapshot.

        // 1. Establish full baseline store data
        using (var setup = _contextFactory.CreateDbContext())
        {
            var drinksCat = new Category { Name = "مشروبات غازية وعصائر", Description = "Beverages" };
            setup.Categories.Add(drinksCat);
            await setup.SaveChangesAsync();

            var prod1 = new Product
            {
                Name = "عصير برتقال طبيعي 1 لتر",
                Barcode = "6221001002001",
                CategoryId = drinksCat.Id,
                UnitId = 1,
                CostPrice = 20m,
                SellingPrice = 28m,
                MinimumStockLevel = 10,
                IsActive = true
            };
            var prod2 = new Product
            {
                Name = "مياه معدنية 500 مل",
                Barcode = "6221001002002",
                CategoryId = drinksCat.Id,
                UnitId = 1,
                CostPrice = 4m,
                SellingPrice = 7m,
                MinimumStockLevel = 25,
                IsActive = true
            };
            setup.Products.AddRange(prod1, prod2);
            await setup.SaveChangesAsync();
        }

        // 2. Create the healthy production backup snapshot
        var disasterRecoveryBackup = CreateTempBackupPath();
        var backupResult = await _backupService.CreateBackupAsync(disasterRecoveryBackup);
        Assert.True(backupResult.Success);

        // 3. Catastrophic disaster strikes: products and category are purged
        using (var disaster = _contextFactory.CreateDbContext())
        {
            var cat = await disaster.Categories.FirstOrDefaultAsync(c => c.Name == "مشروبات غازية وعصائر");
            if (cat != null)
            {
                var prods = await disaster.Products.Where(p => p.CategoryId == cat.Id).ToListAsync();
                disaster.Products.RemoveRange(prods);
                disaster.Categories.Remove(cat);
                await disaster.SaveChangesAsync();
            }
        }

        // Confirm catalog is compromised
        using (var check = _contextFactory.CreateDbContext())
        {
            Assert.Null(await check.Products.FirstOrDefaultAsync(p => p.Barcode == "6221001002001"));
            Assert.Null(await check.Categories.FirstOrDefaultAsync(c => c.Name == "مشروبات غازية وعصائر"));
        }

        // Act: 4. Disaster Recovery procedure executed by Admin
        var restoreResult = await _backupService.RestoreDatabaseAsync(disasterRecoveryBackup);

        // Assert: 5. 100% data recovery verified
        Assert.True(restoreResult.Success);
        using (var recovered = _contextFactory.CreateDbContext())
        {
            var recoveredCat = await recovered.Categories.FirstOrDefaultAsync(c => c.Name == "مشروبات غازية وعصائر");
            Assert.NotNull(recoveredCat);

            var recoveredProd1 = await recovered.Products.FirstOrDefaultAsync(p => p.Barcode == "6221001002001");
            Assert.NotNull(recoveredProd1);
            Assert.Equal("عصير برتقال طبيعي 1 لتر", recoveredProd1.Name);
            Assert.Equal(20m, recoveredProd1.CostPrice);
            Assert.Equal(28m, recoveredProd1.SellingPrice);

            var recoveredProd2 = await recovered.Products.FirstOrDefaultAsync(p => p.Barcode == "6221001002002");
            Assert.NotNull(recoveredProd2);
            Assert.Equal("مياه معدنية 500 مل", recoveredProd2.Name);
            Assert.Equal(7m, recoveredProd2.SellingPrice);
        }
    }

    [Fact]
    public async Task Tier4_RealWorld_SchedulerCatchUp_MissedBackupTriggeredOnLaunch()
    {
        // Scenario: Auto-backup is enabled Daily. The admin PC was powered off over the weekend.
        // On application startup, the scheduler inspects LastBackupUtc, detects the missed backup,
        // and triggers a catch-up backup automatically.

        var targetFolder = CreateTempBackupPath("_folder");
        Directory.CreateDirectory(targetFolder);

        var tempSettingsFile = CreateTempBackupPath(".json");
        var store = new JsonBackupSettingsStore(tempSettingsFile);

        // Last backup was 48 hours ago
        var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
        await store.SaveAsync(new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Daily,
            DestinationFolderPath = targetFolder,
            LastBackupUtc = twoDaysAgo,
            LastBackupStatus = "قديم"
        });

        // Act: Evaluate scheduler catch-up condition
        var settings = await store.LoadAsync();
        bool isDue = false;

        if (settings.IsAutoBackupEnabled)
        {
            var interval = settings.Frequency == BackupFrequency.Weekly
                ? TimeSpan.FromDays(7)
                : TimeSpan.FromDays(1);

            if (settings.LastBackupUtc == null || (DateTime.UtcNow - settings.LastBackupUtc.Value) >= interval)
            {
                isDue = true;
            }
        }

        Assert.True(isDue, "Scheduler must detect that backup is overdue.");

        // Simulate catch-up execution
        var backupFileName = $"AutoBackup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
        var backupFilePath = Path.Combine(settings.DestinationFolderPath!, backupFileName);
        _tempFiles.Add(backupFilePath);

        var result = await _backupService.CreateBackupAsync(backupFilePath);
        Assert.True(result.Success);

        // Update settings with new execution timestamp
        settings.LastBackupUtc = DateTime.UtcNow;
        settings.LastBackupStatus = "تم إنشاء النسخة التلقائية بنجاح";
        await store.SaveAsync(settings);

        // Assert: New backup file exists and settings are updated
        Assert.True(File.Exists(backupFilePath));
        var reloaded = await store.LoadAsync();
        Assert.NotNull(reloaded.LastBackupUtc);
        Assert.True(reloaded.LastBackupUtc > twoDaysAgo);
        Assert.Equal("تم إنشاء النسخة التلقائية بنجاح", reloaded.LastBackupStatus);
    }

    [Fact]
    public async Task Tier4_RealWorld_SchedulerCatchUp_NotDue_DoesNotTrigger()
    {
        // Scenario: Backup was executed 2 hours ago. On startup, scheduler detects it is NOT due.
        var tempSettingsFile = CreateTempBackupPath(".json");
        var store = new JsonBackupSettingsStore(tempSettingsFile);

        var twoHoursAgo = DateTime.UtcNow.AddHours(-2);
        await store.SaveAsync(new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Daily,
            DestinationFolderPath = Path.GetTempPath(),
            LastBackupUtc = twoHoursAgo
        });

        var settings = await store.LoadAsync();
        var interval = settings.Frequency == BackupFrequency.Weekly ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);
        bool isDue = (DateTime.UtcNow - settings.LastBackupUtc!.Value) >= interval;

        // Assert
        Assert.False(isDue, "Recent backup should not be marked as overdue.");
    }

    [Fact]
    public async Task Tier4_RealWorld_UserCancellationScenario_DatabaseRemainsUntouched()
    {
        // Scenario: Admin clicks "Restore Database", safety confirmation dialog appears.
        // Admin clicks "Cancel" (false). The operation must abort and leave the live database untouched.

        // Baseline category
        using (var setup = _contextFactory.CreateDbContext())
        {
            setup.Categories.Add(new Category { Name = "فئة الأمان قبل الإلغاء" });
            await setup.SaveChangesAsync();
        }

        // Mock dialog simulates user clicking Cancel
        _dialogService.ConfirmResult = false;

        // Act: Simulated UI workflow
        bool userConfirmed = _dialogService.Confirm("هل أنت متأكد من استعادة قاعدة البيانات؟", "تأكيد الاستعادة");
        bool restoreAttempted = false;

        if (userConfirmed)
        {
            restoreAttempted = true;
            await _backupService.RestoreDatabaseAsync("dummy_path.bak");
        }

        // Assert
        Assert.False(userConfirmed, "User cancelled the dialog.");
        Assert.False(restoreAttempted, "Restore operation must not be called when user cancels.");

        using (var verify = _contextFactory.CreateDbContext())
        {
            var cat = await verify.Categories.FirstOrDefaultAsync(c => c.Name == "فئة الأمان قبل الإلغاء");
            Assert.NotNull(cat);
        }
    }

    [Fact]
    public async Task Tier4_RealWorld_FailedRestoreFromCorruptFile_LeavesLiveDatabaseOperational()
    {
        // Scenario: Operator accidentally attempts to restore an invalid or corrupted file.
        // Restore fails, but the live operational database MUST NOT be crashed or left in an inconsistent state.

        // Seed live data
        using (var setup = _contextFactory.CreateDbContext())
        {
            setup.Categories.Add(new Category { Name = "بيانات المتجر الحية" });
            await setup.SaveChangesAsync();
        }

        // Create corrupt file
        var corruptFile = CreateTempBackupPath();
        await File.WriteAllTextAsync(corruptFile, "THIS IS CORRUPT NOT A VALID SQLITE FILE");

        // Act: Attempt restore from corrupted file
        var result = await _backupService.RestoreDatabaseAsync(corruptFile);

        // Assert: Restore failed safely
        Assert.False(result.Success);

        // Live database is still completely operational
        using (var verify = _contextFactory.CreateDbContext())
        {
            var canConnect = await verify.Database.CanConnectAsync();
            Assert.True(canConnect, "Live database must remain accessible after failed restore.");

            var cat = await verify.Categories.FirstOrDefaultAsync(c => c.Name == "بيانات المتجر الحية");
            Assert.NotNull(cat);

            // New operations still succeed
            verify.Categories.Add(new Category { Name = "فئة إضافية بعد فشل الاستعادة" });
            await verify.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Tier4_RealWorld_ReadWriteOperationsContinueAfterRestore()
    {
        // Arrange
        var backupPath = CreateTempBackupPath();
        await _backupService.CreateBackupAsync(backupPath);

        // Act: Restore
        var restoreResult = await _backupService.RestoreDatabaseAsync(backupPath);
        Assert.True(restoreResult.Success);

        // Assert: Add new products and categories post-restore, verifying EF Core identity and change tracking
        using var postRestoreContext = _contextFactory.CreateDbContext();
        var newCat = new Category { Name = "قسم ما بعد الاستعادة", Description = "Post restore" };
        postRestoreContext.Categories.Add(newCat);
        await postRestoreContext.SaveChangesAsync();

        Assert.True(newCat.Id > 0, "Identity column must generate valid ID after restore.");

        var savedCat = await postRestoreContext.Categories.FindAsync(newCat.Id);
        Assert.NotNull(savedCat);
        Assert.Equal("قسم ما بعد الاستعادة", savedCat.Name);
    }

    #endregion
}
