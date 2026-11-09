namespace Supermarket.Core.Interfaces;

/// <summary>
/// Result of a database backup operation.
/// </summary>
public sealed record BackupResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? OutputPath { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime? TimestampUtc { get; init; }

    public static BackupResult Succeeded(string outputPath, long fileSizeBytes, DateTime timestampUtc) =>
        new() { Success = true, OutputPath = outputPath, FileSizeBytes = fileSizeBytes, TimestampUtc = timestampUtc };

    public static BackupResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Result of a database restore operation.
/// </summary>
public sealed record RestoreResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static RestoreResult Succeeded() => new() { Success = true };
    public static RestoreResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Operational settings for scheduled database backups, persisted machine-locally.
/// </summary>
public sealed class BackupSettings
{
    public bool IsAutoBackupEnabled { get; set; } = false;
    public BackupFrequency Frequency { get; set; } = BackupFrequency.Daily;
    public string? DestinationFolderPath { get; set; }
    public DateTime? LastBackupUtc { get; set; }
    public string? LastBackupStatus { get; set; }
}

/// <summary>
/// Frequency for automated database backups.
/// </summary>
public enum BackupFrequency
{
    Daily,
    Weekly
}
