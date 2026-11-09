namespace Supermarket.Core.Interfaces;

/// <summary>
/// Service responsible for executing database backup and recovery operations safely
/// across active EF Core contexts and connection pools.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Safely creates a verified database backup at the specified destination file path.
    /// </summary>
    Task<BackupResult> CreateBackupAsync(string destinationFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely restores the database from the specified backup file path, severing active
    /// connections and clearing connection pools.
    /// </summary>
    Task<RestoreResult> RestoreDatabaseAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a file path or directory is valid and accessible for backup operations.
    /// </summary>
    Task<bool> CanWriteToPathAsync(string destinationPath);
}
