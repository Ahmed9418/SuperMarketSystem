using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.Services;

/// <summary>
/// Background scheduler interface responsible for managing automated database backups
/// using .NET 8 PeriodicTimer on ThreadPool workers with missed-schedule catch-up.
/// </summary>
public interface IAutoBackupScheduler : IDisposable
{
    /// <summary>
    /// Gets whether the background scheduler loop is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the background scheduler loop.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops the background scheduler loop.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Immediately triggers an automated backup operation using current settings.
    /// </summary>
    Task TriggerNowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates scheduler memory with newly saved backup settings.
    /// </summary>
    void ReloadSchedule(BackupSettings settings);

    /// <summary>
    /// Raised whenever a scheduled or caught-up auto-backup completes.
    /// </summary>
    event EventHandler<BackupResult>? AutoBackupCompleted;
}
