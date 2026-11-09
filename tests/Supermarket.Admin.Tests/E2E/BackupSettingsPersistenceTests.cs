using System.IO;
using System.Text.Json;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Admin.Tests.E2E;

/// <summary>
/// E2E & Integration Tests for Database Backup Settings Persistence (<see cref="IBackupSettingsStore"/>).
/// Tests cover Tiers 1 through 3: Auto-Backup Toggle, Frequency Selection, Destination Folder Path,
/// boundary/corruption resilience, and cross-feature persistence across process lifetimes.
/// </summary>
public sealed class BackupSettingsPersistenceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
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

    private string CreateIsolatedSettingsPath(string? extension = ".json")
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);

        var path = Path.Combine(tempDir, $"backup_settings{extension}");
        _tempFiles.Add(path);
        return path;
    }

    #region Tier 1 - Feature Coverage: Auto-Backup Toggle

    [Fact]
    public async Task Tier1_AutoBackupToggle_DefaultStateIsDisabled()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled, "Default auto-backup state must be disabled (false).");
    }

    [Fact]
    public async Task Tier1_AutoBackupToggle_EnableSavesSuccessfully()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Act
        var settings = await store.LoadAsync();
        settings.IsAutoBackupEnabled = true;
        await store.SaveAsync(settings);

        var reloaded = await store.LoadAsync();

        // Assert
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsAutoBackupEnabled, "Auto-backup must be enabled after saving true.");
    }

    [Fact]
    public async Task Tier1_AutoBackupToggle_DisableSavesSuccessfully()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Act: First enable, then disable
        var settings = new BackupSettings { IsAutoBackupEnabled = true };
        await store.SaveAsync(settings);

        settings.IsAutoBackupEnabled = false;
        await store.SaveAsync(settings);

        var reloaded = await store.LoadAsync();

        // Assert
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsAutoBackupEnabled, "Auto-backup must be disabled after saving false.");
    }

    [Fact]
    public async Task Tier1_AutoBackupToggle_MultipleTogglesPersistCorrectFinalState()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Act: Sequence of rapid toggle operations
        for (int i = 0; i < 5; i++)
        {
            var s = await store.LoadAsync();
            s.IsAutoBackupEnabled = (i % 2 == 0); // true, false, true, false, true
            await store.SaveAsync(s);
        }

        var finalSettings = await store.LoadAsync();

        // Assert: 4th iteration (i=4) is even -> true
        Assert.True(finalSettings.IsAutoBackupEnabled);
    }

    [Fact]
    public async Task Tier1_AutoBackupToggle_PersistsAcrossFreshStoreInstances()
    {
        // Arrange - Instance A writes settings
        var filePath = CreateIsolatedSettingsPath();
        var storeInstanceA = new JsonBackupSettingsStore(filePath);

        var settings = new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Daily,
            DestinationFolderPath = @"C:\SupermarketBackups"
        };
        await storeInstanceA.SaveAsync(settings);

        // Act - Fresh Instance B (simulating application restart) reads file
        var storeInstanceB = new JsonBackupSettingsStore(filePath);
        var loaded = await storeInstanceB.LoadAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.True(loaded.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Daily, loaded.Frequency);
        Assert.Equal(@"C:\SupermarketBackups", loaded.DestinationFolderPath);
    }

    #endregion

    #region Tier 1 - Feature Coverage: Backup Frequency Selection

    [Fact]
    public async Task Tier1_FrequencySelection_DefaultIsDaily()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.Equal(BackupFrequency.Daily, settings.Frequency);
    }

    [Fact]
    public async Task Tier1_FrequencySelection_ChangeToWeeklyPersists()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Act
        var settings = await store.LoadAsync();
        settings.Frequency = BackupFrequency.Weekly;
        await store.SaveAsync(settings);

        var freshStore = new JsonBackupSettingsStore(filePath);
        var loaded = await freshStore.LoadAsync();

        // Assert
        Assert.Equal(BackupFrequency.Weekly, loaded.Frequency);
    }

    [Fact]
    public async Task Tier1_FrequencySelection_ChangeBackToDailyPersists()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        var settings = new BackupSettings { Frequency = BackupFrequency.Weekly };
        await store.SaveAsync(settings);

        // Act: Switch back to daily
        settings.Frequency = BackupFrequency.Daily;
        await store.SaveAsync(settings);

        var loaded = await store.LoadAsync();

        // Assert
        Assert.Equal(BackupFrequency.Daily, loaded.Frequency);
    }

    [Fact]
    public async Task Tier1_FrequencySelection_EnumSerializationPreservesExactValue()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        var settings = new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Weekly
        };

        // Act
        await store.SaveAsync(settings);
        var jsonText = await File.ReadAllTextAsync(filePath);

        // Assert: Ensure file exists and can be parsed as valid JSON containing the frequency
        Assert.True(File.Exists(filePath));
        Assert.False(string.IsNullOrWhiteSpace(jsonText));

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("Frequency", out var freqProp) || root.TryGetProperty("frequency", out freqProp));

        var loaded = await store.LoadAsync();
        Assert.Equal(BackupFrequency.Weekly, loaded.Frequency);
    }

    [Fact]
    public async Task Tier1_FrequencySelection_PreservedWhenToggleStateChanges()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        var settings = new BackupSettings
        {
            IsAutoBackupEnabled = false,
            Frequency = BackupFrequency.Weekly
        };
        await store.SaveAsync(settings);

        // Act: Toggle auto backup on, without explicitly modifying frequency
        var loaded = await store.LoadAsync();
        loaded.IsAutoBackupEnabled = true;
        await store.SaveAsync(loaded);

        var reloaded = await store.LoadAsync();

        // Assert: Frequency must still be Weekly
        Assert.True(reloaded.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Weekly, reloaded.Frequency);
    }

    #endregion

    #region Tier 1 - Feature Coverage: Destination Folder Path Selection

    [Fact]
    public async Task Tier1_DestinationFolder_DefaultIsNull()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.Null(settings.DestinationFolderPath);
    }

    [Fact]
    public async Task Tier1_DestinationFolder_ValidCustomPathPersists()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);
        var customFolder = Path.Combine(Path.GetTempPath(), "CustomSupermarketBackups");

        // Act
        var settings = await store.LoadAsync();
        settings.DestinationFolderPath = customFolder;
        await store.SaveAsync(settings);

        var reloaded = await new JsonBackupSettingsStore(filePath).LoadAsync();

        // Assert
        Assert.Equal(customFolder, reloaded.DestinationFolderPath);
    }

    [Fact]
    public async Task Tier1_DestinationFolder_DirectoryAutoCreatedWhenMissing()
    {
        // Arrange: Place settings file in nested, non-existent directories
        var nestedDir = Path.Combine(Path.GetTempPath(), $"nested_a_{Guid.NewGuid():N}", $"nested_b_{Guid.NewGuid():N}");
        _tempDirectories.Add(nestedDir);
        var filePath = Path.Combine(nestedDir, "backup_settings.json");
        _tempFiles.Add(filePath);

        var store = new JsonBackupSettingsStore(filePath);

        // Act
        var settings = new BackupSettings { IsAutoBackupEnabled = true };
        await store.SaveAsync(settings);

        // Assert: Parent directories must be auto-created
        Assert.True(Directory.Exists(nestedDir));
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task Tier1_DestinationFolder_PathWithSpacesAndArabicCharacters()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);
        var arabicPath = @"C:\النسخ الاحتياطية للسوبر ماركت\مجلد الطوارئ 2026";

        // Act
        var settings = new BackupSettings
        {
            DestinationFolderPath = arabicPath,
            LastBackupStatus = "جاهز للعمل"
        };
        await store.SaveAsync(settings);

        var reloaded = await new JsonBackupSettingsStore(filePath).LoadAsync();

        // Assert: Full Unicode fidelity preserved
        Assert.Equal(arabicPath, reloaded.DestinationFolderPath);
        Assert.Equal("جاهز للعمل", reloaded.LastBackupStatus);
    }

    [Fact]
    public async Task Tier1_DestinationFolder_ClearingPathPersistsNullOrEmpty()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        var settings = new BackupSettings { DestinationFolderPath = @"D:\Backups" };
        await store.SaveAsync(settings);

        // Act: Clear path
        settings.DestinationFolderPath = null;
        await store.SaveAsync(settings);

        var reloaded = await store.LoadAsync();

        // Assert
        Assert.Null(reloaded.DestinationFolderPath);
    }

    #endregion

    #region Tier 2 - Boundary & Corner Cases

    [Fact]
    public async Task Tier2_Settings_NonExistentSettingsFileReturnsSafeDefaults()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.json");
        var store = new JsonBackupSettingsStore(nonExistentPath);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Daily, settings.Frequency);
        Assert.Null(settings.DestinationFolderPath);
    }

    [Fact]
    public async Task Tier2_Settings_CorruptJsonContentFallsBackToSafeDefaults()
    {
        // Arrange: Write malformed, broken JSON
        var filePath = CreateIsolatedSettingsPath();
        await File.WriteAllTextAsync(filePath, "{ invalid json content @#%& missing braces");

        var store = new JsonBackupSettingsStore(filePath);

        // Act - Must not throw; must gracefully recover
        var settings = await store.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Daily, settings.Frequency);
    }

    [Fact]
    public async Task Tier2_Settings_EmptyJsonFileFallsBackToSafeDefaults()
    {
        // Arrange: 0-byte file
        var filePath = CreateIsolatedSettingsPath();
        await File.WriteAllTextAsync(filePath, string.Empty);

        var store = new JsonBackupSettingsStore(filePath);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled);
    }

    [Fact]
    public async Task Tier2_Settings_WhitespaceOnlyJsonFileFallsBackToSafeDefaults()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        await File.WriteAllTextAsync(filePath, "    \r\n\t   ");

        var store = new JsonBackupSettingsStore(filePath);

        // Act
        var settings = await store.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.IsAutoBackupEnabled);
    }

    [Fact]
    public async Task Tier2_Settings_ConcurrentLoadsAndSavesAreThreadSafe()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Initialize file
        await store.SaveAsync(new BackupSettings { IsAutoBackupEnabled = false });

        // Act: 10 concurrent tasks alternating between reading and updating
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var s = await store.LoadAsync();
            s.IsAutoBackupEnabled = (i % 2 == 0);
            s.DestinationFolderPath = $@"C:\Folder_{i}";
            await store.SaveAsync(s);
        });

        // Assert: No unhandled IOException or sharing violations
        await Task.WhenAll(tasks);

        var finalSettings = await store.LoadAsync();
        Assert.NotNull(finalSettings);
        Assert.NotNull(finalSettings.DestinationFolderPath);
    }

    #endregion

    #region Tier 3 - Cross-Feature Interactions & Decoupling

    [Fact]
    public async Task Tier3_Settings_AllPropertiesUpdatedTogether_PersistAtomically()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        var now = DateTime.UtcNow;
        var fullSettings = new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Weekly,
            DestinationFolderPath = @"E:\SupermarketStore\ProductionBackups",
            LastBackupUtc = now,
            LastBackupStatus = "تم إتمام النسخة الاحتياطية بنجاح بدون أخطاء"
        };

        // Act
        await store.SaveAsync(fullSettings);

        var freshStore = new JsonBackupSettingsStore(filePath);
        var loaded = await freshStore.LoadAsync();

        // Assert: Verify all properties were persisted atomically
        Assert.NotNull(loaded);
        Assert.True(loaded.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Weekly, loaded.Frequency);
        Assert.Equal(@"E:\SupermarketStore\ProductionBackups", loaded.DestinationFolderPath);
        Assert.Equal(now.ToString("u"), loaded.LastBackupUtc?.ToString("u"));
        Assert.Equal("تم إتمام النسخة الاحتياطية بنجاح بدون أخطاء", loaded.LastBackupStatus);
    }

    [Fact]
    public async Task Tier3_Settings_StatusAndTimestampTracking_PreservedWithToggleAndFrequency()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        var timestamp = new DateTime(2026, 11, 9, 10, 30, 0, DateTimeKind.Utc);
        var original = new BackupSettings
        {
            IsAutoBackupEnabled = true,
            Frequency = BackupFrequency.Daily,
            LastBackupUtc = timestamp,
            LastBackupStatus = "نجاح"
        };
        await store.SaveAsync(original);

        // Act: Operator changes only frequency in UI
        var settingsToUpdate = await store.LoadAsync();
        settingsToUpdate.Frequency = BackupFrequency.Weekly;
        await store.SaveAsync(settingsToUpdate);

        var reloaded = await store.LoadAsync();

        // Assert: Timestamp and status must not be lost when modifying frequency
        Assert.Equal(BackupFrequency.Weekly, reloaded.Frequency);
        Assert.Equal(timestamp.ToString("u"), reloaded.LastBackupUtc?.ToString("u"));
        Assert.Equal("نجاح", reloaded.LastBackupStatus);
    }

    [Fact]
    public async Task Tier3_Settings_MultipleSequentialModifications_PreserveHistory()
    {
        // Arrange
        var filePath = CreateIsolatedSettingsPath();
        var store = new JsonBackupSettingsStore(filePath);

        // Step 1: Initial creation
        await store.SaveAsync(new BackupSettings
        {
            IsAutoBackupEnabled = false,
            Frequency = BackupFrequency.Daily,
            DestinationFolderPath = @"C:\Backups1"
        });

        // Step 2: Enable auto backup
        var step2 = await store.LoadAsync();
        step2.IsAutoBackupEnabled = true;
        await store.SaveAsync(step2);

        // Step 3: Switch to weekly and change path
        var step3 = await store.LoadAsync();
        step3.Frequency = BackupFrequency.Weekly;
        step3.DestinationFolderPath = @"D:\Backups2";
        await store.SaveAsync(step3);

        // Step 4: Record a simulated backup run
        var step4 = await store.LoadAsync();
        step4.LastBackupUtc = DateTime.UtcNow;
        step4.LastBackupStatus = "اكتمل بنجاح";
        await store.SaveAsync(step4);

        // Final verification
        var finalSettings = await new JsonBackupSettingsStore(filePath).LoadAsync();
        Assert.True(finalSettings.IsAutoBackupEnabled);
        Assert.Equal(BackupFrequency.Weekly, finalSettings.Frequency);
        Assert.Equal(@"D:\Backups2", finalSettings.DestinationFolderPath);
        Assert.NotNull(finalSettings.LastBackupUtc);
        Assert.Equal("اكتمل بنجاح", finalSettings.LastBackupStatus);
    }

    #endregion
}
