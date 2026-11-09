using System.Text.Json;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Machine-local JSON file implementation of <see cref="IBackupSettingsStore"/>.
/// Persists auto-backup settings outside the database to prevent settings wipeout during database restoration.
/// </summary>
public sealed class JsonBackupSettingsStore : IBackupSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly ILogger<JsonBackupSettingsStore>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// DI constructor defaulting to <c>%LocalAppData%\SupermarketSystem\backup_settings.json</c>.
    /// </summary>
    public JsonBackupSettingsStore(ILogger<JsonBackupSettingsStore>? logger = null)
        : this(GetDefaultSettingsFilePath(), logger)
    {
    }

    /// <summary>
    /// Explicit constructor allowing custom file path (primarily for unit tests and isolation).
    /// </summary>
    public JsonBackupSettingsStore(string filePath, ILogger<JsonBackupSettingsStore>? logger = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? GetDefaultSettingsFilePath()
            : filePath;
        _logger = logger;
    }

    /// <summary>
    /// Gets the standard local file path: <c>%LocalAppData%\SupermarketSystem\backup_settings.json</c>.
    /// </summary>
    public static string GetDefaultSettingsFilePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SupermarketSystem", "backup_settings.json");

    /// <inheritdoc />
    public async Task<BackupSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                return new BackupSettings();
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new BackupSettings();
            }

            var settings = JsonSerializer.Deserialize<BackupSettings>(json, SerializerOptions);
            return settings ?? new BackupSettings();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load backup settings from {Path}. Falling back to default settings.", _filePath);
            return new BackupSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(BackupSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken);
        string? tempFile = null;
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            tempFile = $"{_filePath}.tmp_{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(tempFile, json, cancellationToken);

            File.Move(tempFile, _filePath, overwrite: true);
            tempFile = null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save backup settings to {Path}", _filePath);
            throw;
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* ignore temp cleanup */ }
            }
            _gate.Release();
        }
    }
}
