using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Supermarket.Core.Data;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

public sealed class Challenger1StressTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

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

    private string GetTempPath(string extension = ".bak")
    {
        var path = Path.Combine(Path.GetTempPath(), $"chal1_{Guid.NewGuid():N}{extension}");
        _tempFiles.Add(path);
        return path;
    }

    private string GetTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"chal1_dir_{Guid.NewGuid():N}");
        _tempDirectories.Add(dir);
        return dir;
    }

    private (SqliteTestDatabase db, TestContextFactory factory, BackupService service) CreateInMemoryHarness()
    {
        var db = new SqliteTestDatabase();
        var factory = new TestContextFactory(db);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = "Data Source=:memory:"
            })
            .Build();
        var service = new BackupService(factory, config, TestServices.Logger<BackupService>());
        return (db, factory, service);
    }

    private (string dbPath, PhysicalFileContextFactory factory, BackupService service) CreatePhysicalHarness()
    {
        var dbPath = GetTempPath(".db");
        var connStr = $"Data Source={dbPath}";
        var options = new DbContextOptionsBuilder<SupermarketDbContext>()
            .UseSqlite(connStr)
            .Options;
        var currentUser = new PhysicalTestCurrentUser();

        using (var initCtx = new SqliteSupermarketDbContext(options, currentUser))
        {
            initCtx.Database.EnsureCreated();
        }

        var factory = new PhysicalFileContextFactory(options, currentUser);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = connStr
            })
            .Build();
        var service = new BackupService(factory, config, TestServices.Logger<BackupService>());
        return (dbPath, factory, service);
    }

    #region Area 1: Concurrency & Stress

    [Fact]
    public async Task Concurrency_10SimultaneousBackups_AllSucceedWithDistinctValidFiles()
    {
        var (harness, factory, service) = CreateInMemoryHarness();
        using (harness)
        {
            // Seed some data
            harness.Context.Categories.Add(new Category { Name = "ConcurrentCat", Description = "Testing concurrency" });
            await harness.Context.SaveChangesAsync();

            var backupPaths = Enumerable.Range(0, 10).Select(_ => GetTempPath()).ToList();

            // Launch 10 simultaneous backup tasks
            var tasks = backupPaths.Select(path => service.CreateBackupAsync(path)).ToList();
            var results = await Task.WhenAll(tasks);

            for (int i = 0; i < 10; i++)
            {
                var res = results[i];
                Assert.True(res.Success, $"Backup {i} failed: {res.ErrorMessage}");
                Assert.Null(res.ErrorMessage);
                Assert.True(File.Exists(backupPaths[i]), $"Backup file {i} was not created");
                Assert.True(new FileInfo(backupPaths[i]).Length > 0, $"Backup file {i} is empty");

                // Verify the backup file itself is a valid SQLite DB with the seeded category
                using var standaloneConn = new SqliteConnection($"Data Source={backupPaths[i]}");
                await standaloneConn.OpenAsync();
                using var cmd = standaloneConn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Categories WHERE Name = 'ConcurrentCat';";
                var count = (long)(await cmd.ExecuteScalarAsync())!;
                Assert.Equal(1, count);
            }
        }
    }

    [Fact]
    public async Task Concurrency_SimultaneousBackupAndRestore_SerializedWithoutExceptions()
    {
        var (dbPath, factory, service) = CreatePhysicalHarness();

        using (var ctx = factory.CreateDbContext())
        {
            ctx.Categories.Add(new Category { Name = "InitialCat" });
            await ctx.SaveChangesAsync();
        }

        var validBackup = GetTempPath();
        var initialBackupRes = await service.CreateBackupAsync(validBackup);
        Assert.True(initialBackupRes.Success);

        // Run multiple backups and restores concurrently
        var backup1 = GetTempPath();
        var backup2 = GetTempPath();

        var t1 = Task.Run(() => service.CreateBackupAsync(backup1));
        var t2 = Task.Run(() => service.RestoreDatabaseAsync(validBackup));
        var t3 = Task.Run(() => service.CreateBackupAsync(backup2));

        await Task.WhenAll(t1, t2, t3);
        var res1 = await t1;
        var res2 = await t2;
        var res3 = await t3;
        Assert.True(res1.Success, $"t1 failed: {res1.ErrorMessage}");
        Assert.True(res2.Success, $"t2 failed: {res2.ErrorMessage}");
        Assert.True(res3.Success, $"t3 failed: {res3.ErrorMessage}");
    }

    #endregion

    #region Area 2: Standalone Restorability (In-Memory & File-Based)

    [Fact]
    public async Task StandaloneRestorability_InMemoryDb_ProducesDirectlyQueryableSqliteFile()
    {
        var (db, factory, service) = CreateInMemoryHarness();
        using (db)
        {
            var initialCount = await db.Context.Categories.CountAsync();

            // Seed multiple entities
            var cat1 = new Category { Name = "Beverages_" + Guid.NewGuid().ToString("N"), Description = "Drinks" };
            var cat2 = new Category { Name = "Bakery_" + Guid.NewGuid().ToString("N"), Description = "Breads" };
            db.Context.Categories.AddRange(cat1, cat2);
            await db.Context.SaveChangesAsync();

            var backupPath = GetTempPath();
            var result = await service.CreateBackupAsync(backupPath);

            Assert.True(result.Success, $"Backup failed: {result.ErrorMessage}");
            Assert.True(File.Exists(backupPath));

            // Query independently using raw ADO.NET SqliteConnection without EF Core context
            using var directConn = new SqliteConnection($"Data Source={backupPath}");
            await directConn.OpenAsync();

            using var cmd = directConn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Categories;";
            var catCount = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(initialCount + 2, catCount);

            using var cmdCat = directConn.CreateCommand();
            cmdCat.CommandText = "SELECT Description FROM Categories WHERE Name = @name;";
            cmdCat.Parameters.AddWithValue("@name", cat1.Name);
            var desc = (string)(await cmdCat.ExecuteScalarAsync())!;
            Assert.Equal("Drinks", desc);
        }
    }

    [Fact]
    public async Task StandaloneRestorability_FileBasedDb_ProducesDirectlyQueryableSqliteFile()
    {
        var (dbPath, factory, service) = CreatePhysicalHarness();

        var catName = "Bakery_" + Guid.NewGuid().ToString("N");
        using (var ctx = factory.CreateDbContext())
        {
            var cat = new Category { Name = catName, Description = "Fresh Bread" };
            ctx.Categories.Add(cat);
            await ctx.SaveChangesAsync();
        }

        var backupPath = GetTempPath();
        var result = await service.CreateBackupAsync(backupPath);
        Assert.True(result.Success, $"Backup failed: {result.ErrorMessage}");

        // Query the backup file independently
        using var directConn = new SqliteConnection($"Data Source={backupPath}");
        await directConn.OpenAsync();
        using var cmd = directConn.CreateCommand();
        cmd.CommandText = "SELECT Description FROM Categories WHERE Name = @name;";
        cmd.Parameters.AddWithValue("@name", catName);
        var desc = (string)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal("Fresh Bread", desc);
    }

    [Fact]
    public async Task Restoration_Roundtrip_PhysicalDb_RestoresExactState()
    {
        var (dbPath, factory, service) = CreatePhysicalHarness();

        int state1Count;
        var state1CatName = "State1Cat_" + Guid.NewGuid().ToString("N");
        // State 1: 1 category
        using (var ctx = factory.CreateDbContext())
        {
            ctx.Categories.Add(new Category { Name = state1CatName });
            await ctx.SaveChangesAsync();
            state1Count = await ctx.Categories.CountAsync();
        }

        var backup1 = GetTempPath();
        var bRes = await service.CreateBackupAsync(backup1);
        Assert.True(bRes.Success);

        // State 2: Add 5 more categories
        using (var ctx = factory.CreateDbContext())
        {
            for (int i = 0; i < 5; i++)
            {
                ctx.Categories.Add(new Category { Name = $"State2Cat_{i}_{Guid.NewGuid().ToString("N")}" });
            }
            await ctx.SaveChangesAsync();
            Assert.Equal(state1Count + 5, await ctx.Categories.CountAsync());
        }

        // Restore to State 1
        var rRes = await service.RestoreDatabaseAsync(backup1);
        Assert.True(rRes.Success, $"Restore failed: {rRes.ErrorMessage}");

        // Verify state is back to State 1
        using (var ctx = factory.CreateDbContext())
        {
            var count = await ctx.Categories.CountAsync();
            Assert.Equal(state1Count, count);
            var cat = await ctx.Categories.FirstOrDefaultAsync(c => c.Name == state1CatName);
            Assert.NotNull(cat);
        }
    }

    #endregion

    #region Area 3: CanWriteToPathAsync Edge Cases

    [Fact]
    public async Task CanWriteToPathAsync_VariousValidAndInvalidPaths_EvaluatedCorrectly()
    {
        var (db, factory, service) = CreateInMemoryHarness();
        using (db)
        {
            // 1. Existing valid directory
            var tempDir = Path.GetTempPath();
            Assert.True(await service.CanWriteToPathAsync(tempDir));

            // 2. File in existing valid directory
            var tempFile = Path.Combine(tempDir, "sample_probe.bak");
            Assert.True(await service.CanWriteToPathAsync(tempFile));

            // 3. Subdirectory that does not exist yet (should return true and create it)
            var newSubDir = Path.Combine(tempDir, $"probe_subdir_{Guid.NewGuid():N}");
            _tempDirectories.Add(newSubDir);
            var fileInNewSubDir = Path.Combine(newSubDir, "test.bak");
            Assert.True(await service.CanWriteToPathAsync(fileInNewSubDir));
            Assert.True(Directory.Exists(newSubDir));

            // 4. Non-existent drive
            Assert.False(await service.CanWriteToPathAsync(@"Z:\NonExistentDrive_X\backup.bak"));

            // 5. Empty and whitespace
            Assert.False(await service.CanWriteToPathAsync(""));
            Assert.False(await service.CanWriteToPathAsync("   "));

            // 6. Path with invalid filename characters
            var invalidCharsPath = Path.Combine(tempDir, "invalid|file<name>?.bak");
            Assert.False(await service.CanWriteToPathAsync(invalidCharsPath));
        }
    }

    [Fact]
    public async Task CanWriteToPathAsync_CleansUpProbeFiles()
    {
        var (db, factory, service) = CreateInMemoryHarness();
        using (db)
        {
            var tempDir = GetTempDirectory();
            Directory.CreateDirectory(tempDir);

            for (int i = 0; i < 5; i++)
            {
                var canWrite = await service.CanWriteToPathAsync(tempDir);
                Assert.True(canWrite);
            }

            // Probe files must have been deleted, leaving directory clean of .probe_*.tmp
            var probeFiles = Directory.GetFiles(tempDir, ".probe_*.tmp");
            Assert.Empty(probeFiles);
        }
    }

    #endregion

    #region Area 4: JsonBackupSettingsStore Edge Cases & Concurrency

    [Fact]
    public async Task JsonBackupSettingsStore_MissingDirectory_AutomaticallyCreatesDirectoryOnSave()
    {
        var deepDir = Path.Combine(Path.GetTempPath(), $"chal1_nested_{Guid.NewGuid():N}", "sub1", "sub2");
        _tempDirectories.Add(Path.Combine(Path.GetTempPath(), deepDir.Split(Path.DirectorySeparatorChar)[^3]));
        var settingsFile = Path.Combine(deepDir, "settings.json");

        var store = new JsonBackupSettingsStore(settingsFile);

        var settings = new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Weekly,
            DestinationFolderPath = @"D:\Backups"
        };

        await store.SaveAsync(settings);

        Assert.True(Directory.Exists(deepDir));
        Assert.True(File.Exists(settingsFile));

        var loaded = await store.LoadAsync();
        Assert.True(loaded.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Weekly, loaded.Frequency);
        Assert.Equal(@"D:\Backups", loaded.DestinationFolderPath);
    }

    [Fact]
    public async Task JsonBackupSettingsStore_EmptyAndWhitespaceFile_ReturnsDefaultSettings()
    {
        // 0-byte file
        var emptyFile = GetTempPath(".json");
        await File.WriteAllBytesAsync(emptyFile, Array.Empty<byte>());
        var store1 = new JsonBackupSettingsStore(emptyFile);
        var loaded1 = await store1.LoadAsync();
        Assert.NotNull(loaded1);
        Assert.False(loaded1.IsAutoBackupEnabled);

        // Whitespace only file
        var whitespaceFile = GetTempPath(".json");
        await File.WriteAllTextAsync(whitespaceFile, "   \r\n\t  ");
        var store2 = new JsonBackupSettingsStore(whitespaceFile);
        var loaded2 = await store2.LoadAsync();
        Assert.NotNull(loaded2);
        Assert.False(loaded2.IsAutoBackupEnabled);
    }

    [Fact]
    public async Task JsonBackupSettingsStore_CorruptJson_ReturnsDefaultSettingsWithoutCrashing()
    {
        var corruptFile = GetTempPath(".json");
        await File.WriteAllTextAsync(corruptFile, "{{{{ this is not json !!");
        var store = new JsonBackupSettingsStore(corruptFile);
        var loaded = await store.LoadAsync();
        Assert.NotNull(loaded);
        Assert.False(loaded.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Daily, loaded.Frequency);
    }

    [Fact]
    public async Task JsonBackupSettingsStore_HighContention_ConcurrentReadsAndWrites_RemainConsistent()
    {
        var settingsFile = GetTempPath(".json");
        var store = new JsonBackupSettingsStore(settingsFile);

        // Initial save
        await store.SaveAsync(new BackupSettings { IsAutoBackupEnabled = false, Frequency = BackupFrequency.Daily });

        // 10 concurrent writers and 10 concurrent readers
        var writeTasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            var settings = new BackupSettings
            {
                IsAutoBackupEnabled = i % 2 == 0,
                Frequency = i % 2 == 0 ? BackupFrequency.Weekly : BackupFrequency.Daily,
                DestinationFolderPath = $@"C:\Backups_{i}"
            };
            await store.SaveAsync(settings);
        }));

        var readTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            var loaded = await store.LoadAsync();
            Assert.NotNull(loaded);
        }));

        await Task.WhenAll(writeTasks.Concat(readTasks));

        // Final load must be valid and readable
        var finalLoaded = await store.LoadAsync();
        Assert.NotNull(finalLoaded);
        Assert.NotNull(finalLoaded.DestinationFolderPath);
    }

    #endregion

    #region Helper Classes

    private sealed class PhysicalTestCurrentUser : ICurrentUserService
    {
        public string? UserName => "chal1-user";
    }

    private sealed class PhysicalFileContextFactory : IDbContextFactory<SupermarketDbContext>
    {
        private readonly DbContextOptions<SupermarketDbContext> _options;
        private readonly ICurrentUserService _currentUser;

        public PhysicalFileContextFactory(DbContextOptions<SupermarketDbContext> options, ICurrentUserService currentUser)
        {
            _options = options;
            _currentUser = currentUser;
        }

        public SupermarketDbContext CreateDbContext() => new SqliteSupermarketDbContext(_options, _currentUser);

        public Task<SupermarketDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SupermarketDbContext>(new SqliteSupermarketDbContext(_options, _currentUser));
    }

    #endregion
}
