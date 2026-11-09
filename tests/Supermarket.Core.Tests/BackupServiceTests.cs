using Microsoft.Extensions.Configuration;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database;
    private readonly TestContextFactory _factory;
    private readonly IConfiguration _configuration;
    private readonly BackupService _backupService;
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    public BackupServiceTests()
    {
        _database = new SqliteTestDatabase();
        _factory = new TestContextFactory(_database);

        var configValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SupermarketDb"] = "Data Source=:memory:"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _backupService = new BackupService(_factory, _configuration, TestServices.Logger<BackupService>());
    }

    public void Dispose()
    {
        _database.Dispose();

        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* ignore test cleanup errors */ }
            }
        }

        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* ignore test cleanup errors */ }
            }
        }
    }

    private string GetTempBackupPath(string? extension = ".db")
    {
        var path = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid():N}{extension}");
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task CreateBackupAsync_WithValidPath_CreatesValidBackupFile()
    {
        // Arrange
        var backupPath = GetTempBackupPath();

        // Act
        var result = await _backupService.CreateBackupAsync(backupPath);

        // Assert
        Assert.True(result.Success, $"Backup failed: {result.ErrorMessage}");
        Assert.Null(result.ErrorMessage);
        Assert.Equal(backupPath, result.OutputPath);
        Assert.True(result.FileSizeBytes > 0, "Backup file size should be greater than zero.");
        Assert.NotNull(result.TimestampUtc);
        Assert.True(File.Exists(backupPath), "Backup file was not created on disk.");
        Assert.True(new FileInfo(backupPath).Length > 0, "Backup file on disk is empty.");
    }

    [Fact]
    public async Task CreateBackupAsync_WhenDirectoryDoesNotExist_CreatesDirectoryAndBackup()
    {
        // Arrange
        var tempSubDir = Path.Combine(Path.GetTempPath(), $"backup_subdir_{Guid.NewGuid():N}");
        _tempDirectories.Add(tempSubDir);
        var backupPath = Path.Combine(tempSubDir, "database.bak");
        _tempFiles.Add(backupPath);

        // Act
        var result = await _backupService.CreateBackupAsync(backupPath);

        // Assert
        Assert.True(result.Success, $"Backup failed: {result.ErrorMessage}");
        Assert.True(Directory.Exists(tempSubDir));
        Assert.True(File.Exists(backupPath));
        Assert.True(result.FileSizeBytes > 0);
    }

    [Fact]
    public async Task CreateBackupAsync_WithInvalidPath_ReturnsFailureWithoutException()
    {
        // Arrange - invalid drive letter
        var invalidPath = @"Z:\NonExistentDrive_XYZ_987654321\backup.bak";

        // Act
        var result = await _backupService.CreateBackupAsync(invalidPath);

        // Assert
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task CreateBackupAsync_WithEmptyPath_ReturnsFailureWithoutException()
    {
        // Act
        var result = await _backupService.CreateBackupAsync(string.Empty);

        // Assert
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task RestoreDatabaseAsync_WithNonExistentFile_ReturnsFailureWithoutException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"missing_backup_{Guid.NewGuid():N}.bak");

        // Act
        var result = await _backupService.RestoreDatabaseAsync(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task RestoreDatabaseAsync_WithEmptyPath_ReturnsFailureWithoutException()
    {
        // Act
        var result = await _backupService.RestoreDatabaseAsync(string.Empty);

        // Assert
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task RestoreDatabaseAsync_WithValidBackup_RestoresDatabaseState()
    {
        // Arrange: 1. Add an initial category to the database
        var initialCategory = new Category
        {
            Name = "InitialCategory_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Category before backup"
        };
        _database.Context.Categories.Add(initialCategory);
        await _database.Context.SaveChangesAsync();

        var initialCount = _database.Context.Categories.Count();
        Assert.True(initialCount >= 1);

        // 2. Create a backup of the current database state
        var backupPath = GetTempBackupPath();
        var backupResult = await _backupService.CreateBackupAsync(backupPath);
        Assert.True(backupResult.Success, $"Initial backup failed: {backupResult.ErrorMessage}");

        // 3. Mutate the database: add a second category
        var postBackupCategory = new Category
        {
            Name = "PostBackupCategory_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Category after backup"
        };
        _database.Context.Categories.Add(postBackupCategory);
        await _database.Context.SaveChangesAsync();

        Assert.Equal(initialCount + 1, _database.Context.Categories.Count());
        Assert.Contains(_database.Context.Categories, c => c.Name == postBackupCategory.Name);

        // Act: 4. Restore the database from the backup
        var restoreResult = await _backupService.RestoreDatabaseAsync(backupPath);

        // Assert
        Assert.True(restoreResult.Success, $"Restore failed: {restoreResult.ErrorMessage}");

        // Verify database state: postBackupCategory should be gone and initialCategory preserved
        using var verifyContext = _database.CreateContext();
        var categoriesAfterRestore = verifyContext.Categories.ToList();

        Assert.Equal(initialCount, categoriesAfterRestore.Count);
        Assert.Contains(categoriesAfterRestore, c => c.Name == initialCategory.Name);
        Assert.DoesNotContain(categoriesAfterRestore, c => c.Name == postBackupCategory.Name);
    }

    [Fact]
    public async Task CanWriteToPathAsync_ValidAndInvalidPaths_EvaluatedCorrectly()
    {
        // Valid directory
        var validTempDir = Path.GetTempPath();
        var canWriteValid = await _backupService.CanWriteToPathAsync(validTempDir);
        Assert.True(canWriteValid);

        // Valid file path in temp directory
        var validTempFile = Path.Combine(validTempDir, "test_file.bak");
        var canWriteValidFile = await _backupService.CanWriteToPathAsync(validTempFile);
        Assert.True(canWriteValidFile);

        // Invalid drive
        var invalidDrive = @"Z:\NonExistentDrive_XYZ_1234567\backup.bak";
        var canWriteInvalid = await _backupService.CanWriteToPathAsync(invalidDrive);
        Assert.False(canWriteInvalid);

        // Invalid filename characters in valid directory
        var invalidFilename = Path.Combine(validTempDir, "invalid|file<name>?.bak");
        var canWriteInvalidFilename = await _backupService.CanWriteToPathAsync(invalidFilename);
        Assert.False(canWriteInvalidFilename);

        // Null and empty
        Assert.False(await _backupService.CanWriteToPathAsync(string.Empty));
        Assert.False(await _backupService.CanWriteToPathAsync("   "));
    }

    [Fact]
    public async Task ConcurrencyGuard_AcrossDifferentInstances_SharesStaticGate()
    {
        // Arrange
        var backupService2 = new BackupService(_factory, _configuration, TestServices.Logger<BackupService>());

        var backupPath1 = GetTempBackupPath();
        var backupPath2 = GetTempBackupPath();

        // Act - launch operations concurrently across distinct BackupService instances
        var task1 = _backupService.CreateBackupAsync(backupPath1);
        var task2 = backupService2.CreateBackupAsync(backupPath2);

        var results = await Task.WhenAll(task1, task2);

        // Assert - both instances serialize safely through the static gate
        Assert.True(results[0].Success);
        Assert.True(results[1].Success);
    }

    [Fact]
    public async Task ConcurrencyGuard_SerializesConcurrentBackupOperations()
    {
        // Arrange
        var backupPath1 = GetTempBackupPath();
        var backupPath2 = GetTempBackupPath();
        var backupPath3 = GetTempBackupPath();

        // Act - launch multiple operations concurrently
        var task1 = _backupService.CreateBackupAsync(backupPath1);
        var task2 = _backupService.CreateBackupAsync(backupPath2);
        var task3 = _backupService.CreateBackupAsync(backupPath3);

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert - all operations complete safely without concurrency failure
        foreach (var res in results)
        {
            Assert.True(res.Success, $"Concurrent backup failed: {res.ErrorMessage}");
            Assert.True(File.Exists(res.OutputPath));
            Assert.True(res.FileSizeBytes > 0);
        }
    }

    [Fact]
    public async Task JsonBackupSettingsStore_DefaultSettings_WhenFileDoesNotExist()
    {
        // Arrange
        var tempSettingsFile = GetTempBackupPath(".json");
        if (File.Exists(tempSettingsFile)) File.Delete(tempSettingsFile);

        var store = new JsonBackupSettingsStore(tempSettingsFile);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Daily, settings.Frequency);
        Assert.Null(settings.DestinationFolderPath);
        Assert.Null(settings.LastBackupUtc);
        Assert.Null(settings.LastBackupStatus);
    }

    [Fact]
    public async Task JsonBackupSettingsStore_SaveAndLoad_RoundTripPreservesAllFields()
    {
        // Arrange
        var tempSettingsFile = GetTempBackupPath(".json");
        var store = new JsonBackupSettingsStore(tempSettingsFile);

        var now = DateTime.UtcNow;
        var originalSettings = new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Weekly,
            DestinationFolderPath = @"C:\SupermarketBackups\Weekly",
            LastBackupUtc = now,
            LastBackupStatus = "تم إنشاء النسخة الاحتياطية بنجاح"
        };

        // Act
        await store.SaveAsync(originalSettings);

        // Read using a separate instance to ensure persistence on disk
        var secondStore = new JsonBackupSettingsStore(tempSettingsFile);
        var loadedSettings = await secondStore.LoadAsync();

        // Assert
        Assert.NotNull(loadedSettings);
        Assert.True(loadedSettings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Weekly, loadedSettings.Frequency);
        Assert.Equal(@"C:\SupermarketBackups\Weekly", loadedSettings.DestinationFolderPath);
        Assert.Equal("تم إنشاء النسخة الاحتياطية بنجاح", loadedSettings.LastBackupStatus);
        Assert.NotNull(loadedSettings.LastBackupUtc);
        Assert.Equal(now.ToString("u"), loadedSettings.LastBackupUtc.Value.ToString("u"));
    }

    [Fact]
    public async Task JsonBackupSettingsStore_CorruptJson_ReturnsDefaultSettingsWithoutCrashing()
    {
        // Arrange
        var tempSettingsFile = GetTempBackupPath(".json");
        await File.WriteAllTextAsync(tempSettingsFile, "{ broken json content !!! not valid json");

        var store = new JsonBackupSettingsStore(tempSettingsFile);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Daily, settings.Frequency);
    }
}
