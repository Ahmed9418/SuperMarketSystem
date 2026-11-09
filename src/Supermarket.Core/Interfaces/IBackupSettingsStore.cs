namespace Supermarket.Core.Interfaces;

/// <summary>
/// Abstraction for persisting and retrieving machine-local database backup settings,
/// decoupled from database lifecycle so restore operations do not overwrite local configurations.
/// </summary>
public interface IBackupSettingsStore
{
    /// <summary>
    /// Asynchronously loads the current backup settings.
    /// Returns default settings if no configuration file exists.
    /// </summary>
    Task<BackupSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously saves the provided backup settings.
    /// </summary>
    Task SaveAsync(BackupSettings settings, CancellationToken cancellationToken = default);
}
