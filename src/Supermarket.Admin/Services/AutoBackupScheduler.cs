using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.Services;

/// <summary>
/// In-app background scheduler implementing <see cref="IAutoBackupScheduler"/> using .NET 8 PeriodicTimer
/// on ThreadPool background workers with missed-schedule catch-up and safe concurrency serialization.
/// </summary>
public sealed class AutoBackupScheduler : IAutoBackupScheduler
{
    private readonly IBackupService _backupService;
    private readonly IBackupSettingsStore _settingsStore;
    private readonly ILogger<AutoBackupScheduler> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _startupDelay;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private BackupSettings? _cachedSettings;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public event EventHandler<BackupResult>? AutoBackupCompleted;

    public AutoBackupScheduler(
        IBackupService backupService,
        IBackupSettingsStore settingsStore,
        ILogger<AutoBackupScheduler>? logger = null,
        TimeSpan? checkInterval = null,
        TimeSpan? startupDelay = null)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _logger = logger ?? NullLogger<AutoBackupScheduler>.Instance;
        _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
        _startupDelay = startupDelay ?? TimeSpan.FromSeconds(3);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        IsRunning = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = Task.Run(() => RunSchedulerLoopAsync(_cts.Token), _cts.Token);

        _logger.LogInformation("AutoBackupScheduler started successfully (Check interval: {Interval}).", _checkInterval);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        _logger.LogInformation("Stopping AutoBackupScheduler...");
        _cts?.Cancel();

        if (_backgroundTask != null)
        {
            try
            {
                await _backgroundTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while awaiting AutoBackupScheduler loop shutdown.");
            }
            finally
            {
                _backgroundTask = null;
            }
        }

        IsRunning = false;
        _logger.LogInformation("AutoBackupScheduler stopped.");
    }

    public async Task TriggerNowAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = _cachedSettings ?? await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(settings.DestinationFolderPath))
            {
                throw new InvalidOperationException("Auto-backup destination folder path is not configured.");
            }

            if (!Directory.Exists(settings.DestinationFolderPath))
            {
                Directory.CreateDirectory(settings.DestinationFolderPath);
            }

            var now = DateTime.UtcNow;
            var fileName = $"SupermarketDb_Auto_{now:yyyyMMdd_HHmmss}.bak";
            var fullPath = Path.Combine(settings.DestinationFolderPath, fileName);

            var result = await _backupService.CreateBackupAsync(fullPath, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                settings.LastBackupUtc = result.TimestampUtc ?? now;
                settings.LastBackupStatus = $"تم بنجاح: {Path.GetFileName(fullPath)} ({now.ToLocalTime():yyyy/MM/dd HH:mm})";
            }
            else
            {
                settings.LastBackupStatus = $"فشل: {result.ErrorMessage}";
            }

            await _settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            _cachedSettings = settings;

            AutoBackupCompleted?.Invoke(this, result);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void ReloadSchedule(BackupSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _cachedSettings = settings;
        _logger.LogInformation("AutoBackupScheduler schedule reloaded. Enabled: {Enabled}, Frequency: {Freq}, Folder: {Folder}",
            settings.IsAutoBackupEnabled, settings.Frequency, settings.DestinationFolderPath);
    }

    private async Task RunSchedulerLoopAsync(CancellationToken ct)
    {
        try
        {
            _cachedSettings = await _settingsStore.LoadAsync(ct).ConfigureAwait(false);

            // Startup grace period
            if (_startupDelay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(_startupDelay, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            // Catch-up check on startup
            await CheckAndExecuteScheduledBackupAsync(isCatchUp: true, ct).ConfigureAwait(false);

            using var timer = new PeriodicTimer(_checkInterval);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                    {
                        break;
                    }

                    await CheckAndExecuteScheduledBackupAsync(isCatchUp: false, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in periodic auto-backup tick.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Clean exit
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in AutoBackupScheduler loop.");
        }
    }

    private async Task CheckAndExecuteScheduledBackupAsync(bool isCatchUp, CancellationToken ct)
    {
        // Try acquiring lock; skip tick if another backup or restore is active
        if (!await _lock.WaitAsync(0, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("Skipping scheduled backup check: lock is already held.");
            return;
        }

        try
        {
            var settings = _cachedSettings ?? await _settingsStore.LoadAsync(ct).ConfigureAwait(false);
            if (!settings.IsAutoBackupEnabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.DestinationFolderPath))
            {
                _logger.LogWarning("Auto-backup is enabled but destination folder is empty.");
                return;
            }

            var interval = settings.Frequency == BackupFrequency.Weekly
                ? TimeSpan.FromDays(7)
                : TimeSpan.FromDays(1);

            var now = DateTime.UtcNow;
            var isDue = !settings.LastBackupUtc.HasValue || (now - settings.LastBackupUtc.Value) >= interval;

            if (!isDue)
            {
                return;
            }

            _logger.LogInformation("Executing {Type} auto-backup. Interval: {Interval}, LastBackupUtc: {LastUtc}",
                isCatchUp ? "missed catch-up" : "periodic", interval, settings.LastBackupUtc);

            if (!Directory.Exists(settings.DestinationFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(settings.DestinationFolderPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create auto-backup directory {Path}.", settings.DestinationFolderPath);
                    return;
                }
            }

            var fileName = $"SupermarketDb_Auto_{now:yyyyMMdd_HHmmss}.bak";
            var fullPath = Path.Combine(settings.DestinationFolderPath, fileName);

            var result = await _backupService.CreateBackupAsync(fullPath, ct).ConfigureAwait(false);

            if (result.Success)
            {
                settings.LastBackupUtc = result.TimestampUtc ?? now;
                settings.LastBackupStatus = $"تم بنجاح: {Path.GetFileName(fullPath)} ({now.ToLocalTime():yyyy/MM/dd HH:mm})";
                _logger.LogInformation("Auto-backup succeeded: {Path}", fullPath);
            }
            else
            {
                settings.LastBackupStatus = $"فشل: {result.ErrorMessage}";
                _logger.LogError("Auto-backup failed: {Error}", result.ErrorMessage);
            }

            await _settingsStore.SaveAsync(settings, ct).ConfigureAwait(false);
            _cachedSettings = settings;

            AutoBackupCompleted?.Invoke(this, result);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _lock.Dispose();
    }
}
