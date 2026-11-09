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

public sealed class AdversarialBackupTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        // Clear sqlite pools so open file handles are released before deleting files
        SqliteConnection.ClearAllPools();

        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { }
            }
        }
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }

    private string CreateTempFile(string extension = ".tmp")
    {
        var path = Path.Combine(Path.GetTempPath(), $"adv_test_{Guid.NewGuid():N}{extension}");
        _tempFiles.Add(path);
        return path;
    }

    private string CreateCorruptFile(int byteCount = 2048)
    {
        var path = CreateTempFile(".bak");
        var rnd = new Random(42);
        var bytes = new byte[byteCount];
        rnd.NextBytes(bytes);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public async Task Adversarial_RestoreCorruptFile_InMemoryDb_EmpiricalResult()
    {
        // Arrange
        using var testDb = new SqliteTestDatabase();
        var factory = new TestContextFactory(testDb);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = "Data Source=:memory:"
            })
            .Build();

        var backupService = new BackupService(factory, config, TestServices.Logger<BackupService>());

        testDb.Context.Categories.Add(new Category { Name = "SafeCategory", Description = "Existing data" });
        await testDb.Context.SaveChangesAsync();

        var corruptFile = CreateCorruptFile(1024);

        // Act
        var result = await backupService.RestoreDatabaseAsync(corruptFile);

        // Empirical check: what does the service actually return?
        // Finding: result.Success is TRUE despite corrupt file!
        Assert.False(result.Success, $"BUG CONFIRMED: BackupService reported Success=true when restoring a corrupt file containing random bytes! ErrorMessage: '{result.ErrorMessage}'");
    }

    [Fact]
    public async Task Adversarial_RestoreCorruptFile_PhysicalDb_EmpiricalResult()
    {
        // Arrange: Create a physical file-backed SQLite database
        var dbPath = CreateTempFile(".db");
        var connStr = $"Data Source={dbPath}";

        var options = new DbContextOptionsBuilder<SupermarketDbContext>()
            .UseSqlite(connStr)
            .Options;

        var currentUser = new PhysicalTestCurrentUser();

        // Initialize and seed physical DB
        using (var initialCtx = new SqliteSupermarketDbContext(options, currentUser))
        {
            await initialCtx.Database.EnsureCreatedAsync();
            initialCtx.Categories.Add(new Category { Name = "PreRestoreCategory", Description = "Must survive corrupt restore" });
            await initialCtx.SaveChangesAsync();
        }

        var factoryMock = new PhysicalFileContextFactory(options, currentUser);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = connStr
            })
            .Build();

        var backupService = new BackupService(factoryMock, config, TestServices.Logger<BackupService>());

        var corruptFile = CreateCorruptFile(2048);

        // Act
        var result = await backupService.RestoreDatabaseAsync(corruptFile);

        // Empirical check: what did it return?
        Assert.False(result.Success, $"BUG CONFIRMED: BackupService reported Success=true on physical DB when restoring a corrupt file! ErrorMessage: '{result.ErrorMessage}'");
    }

    [Fact]
    public async Task Adversarial_RestoreZeroByteFile_MustReturnFailed()
    {
        using var testDb = new SqliteTestDatabase();
        var factory = new TestContextFactory(testDb);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = "Data Source=:memory:"
            })
            .Build();

        var backupService = new BackupService(factory, config, TestServices.Logger<BackupService>());

        var zeroByteFile = CreateTempFile(".bak");
        File.WriteAllBytes(zeroByteFile, Array.Empty<byte>());

        var result = await backupService.RestoreDatabaseAsync(zeroByteFile);

        Assert.False(result.Success, $"BUG CONFIRMED: Restoring from a 0-byte file reported Success=true! ErrorMessage: '{result.ErrorMessage}'");
    }

    [Fact]
    public async Task Adversarial_PreCancelledToken_CreateBackup_ThrowsInsteadOfReturningFailed()
    {
        using var testDb = new SqliteTestDatabase();
        var factory = new TestContextFactory(testDb);
        var config = new ConfigurationBuilder().Build();
        var backupService = new BackupService(factory, config, TestServices.Logger<BackupService>());

        var targetPath = CreateTempFile(".bak");
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        // Act & Assert:
        // Requirement R4: operations must not throw unhandled exceptions.
        // But _gate.WaitAsync(cancellationToken) is outside the try block, so it throws OperationCanceledException!
        var ex = await Record.ExceptionAsync(() => backupService.CreateBackupAsync(targetPath, cts.Token));

        Assert.Null(ex); // If ex is not null, an unhandled exception escaped CreateBackupAsync!
    }

    [Fact]
    public async Task Adversarial_PreCancelledToken_Restore_ThrowsInsteadOfReturningFailed()
    {
        using var testDb = new SqliteTestDatabase();
        var factory = new TestContextFactory(testDb);
        var config = new ConfigurationBuilder().Build();
        var backupService = new BackupService(factory, config, TestServices.Logger<BackupService>());

        var dummyBackup = CreateCorruptFile(100);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        // Act & Assert:
        // Requirement R4: operations must not throw unhandled exceptions.
        var ex = await Record.ExceptionAsync(() => backupService.RestoreDatabaseAsync(dummyBackup, cts.Token));

        Assert.Null(ex); // If ex is not null, an unhandled exception escaped RestoreDatabaseAsync!
    }

    [Fact]
    public async Task Adversarial_BackupToInvalidDrive_MustReturnFailedWithoutCrashing()
    {
        using var testDb = new SqliteTestDatabase();
        var factory = new TestContextFactory(testDb);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = "Data Source=:memory:"
            })
            .Build();

        var backupService = new BackupService(factory, config, TestServices.Logger<BackupService>());

        var invalidPaths = new[]
        {
            @"Z:\NonExistentDrive_X\some\path\backup.bak",
            @"Q:\System32\backup.bak"
        };

        foreach (var path in invalidPaths)
        {
            var result = await backupService.CreateBackupAsync(path);
            Assert.False(result.Success, $"Backup to '{path}' should fail gracefully.");
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        }
    }

    [Fact]
    public async Task Adversarial_RestoreLocksAndConnectionPool_CanDeleteBackupAfterRestore()
    {
        // Ensure no lock is retained on the backup file after restore
        using var testDb = new SqliteTestDatabase();
        var factory = new TestContextFactory(testDb);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SupermarketDb"] = "Data Source=:memory:"
            })
            .Build();

        var backupService = new BackupService(factory, config, TestServices.Logger<BackupService>());

        var backupPath = CreateTempFile(".bak");
        var backupResult = await backupService.CreateBackupAsync(backupPath);
        Assert.True(backupResult.Success);

        var restoreResult = await backupService.RestoreDatabaseAsync(backupPath);
        Assert.True(restoreResult.Success);

        // Verification: The backup file should be immediately deletable without file lock errors
        Exception? deleteException = null;
        try
        {
            File.Delete(backupPath);
        }
        catch (Exception ex)
        {
            deleteException = ex;
        }

        Assert.Null(deleteException);
    }

    private sealed class PhysicalTestCurrentUser : ICurrentUserService
    {
        public string? UserName => "physical-test";
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
}
