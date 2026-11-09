using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Data;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Production-grade database backup and recovery service supporting both Microsoft SQL Server
/// (via native T-SQL commands with master isolation, single-user termination and connection pool clearing)
/// and SQLite (via native backup API and VACUUM INTO for the test harness).
/// All operations are concurrency-guarded via SemaphoreSlim(1,1).
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly IDbContextFactory<SupermarketDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupService> _logger;
    private static readonly SemaphoreSlim _gate = new(1, 1);

    public BackupService(
        IDbContextFactory<SupermarketDbContext> dbContextFactory,
        IConfiguration configuration,
        ILogger<BackupService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<BackupResult> CreateBackupAsync(string destinationFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            return BackupResult.Failed("Destination file path cannot be empty.");
        }

        try
        {
            var dir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid destination path: {Path}", destinationFilePath);
            return BackupResult.Failed($"Invalid destination directory: {ex.Message}");
        }

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Backup operation was cancelled.");
            return BackupResult.Failed("Backup operation was cancelled.");
        }

        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var provider = context.Database.ProviderName;

            if (provider != null && provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateSqlServerBackupAsync(context, destinationFilePath, cancellationToken).ConfigureAwait(false);
            }
            else if (provider != null && provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateSqliteBackupAsync(context, destinationFilePath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var msg = $"Unsupported database provider: {provider}";
                _logger.LogError(msg);
                return BackupResult.Failed(msg);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Backup operation was cancelled.");
            return BackupResult.Failed("Backup operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database backup failed for destination {Path}", destinationFilePath);
            return BackupResult.Failed(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RestoreResult> RestoreDatabaseAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
        {
            return RestoreResult.Failed("Backup file path cannot be empty.");
        }

        if (!File.Exists(backupFilePath))
        {
            _logger.LogError("Backup file not found: {Path}", backupFilePath);
            return RestoreResult.Failed($"Backup file does not exist: {backupFilePath}");
        }

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Restore operation was cancelled.");
            return RestoreResult.Failed("Restore operation was cancelled.");
        }

        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var provider = context.Database.ProviderName;

            if (provider != null && provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                return await RestoreSqlServerDatabaseAsync(context, backupFilePath, cancellationToken).ConfigureAwait(false);
            }
            else if (provider != null && provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                return await RestoreSqliteDatabaseAsync(context, backupFilePath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var msg = $"Unsupported database provider: {provider}";
                _logger.LogError(msg);
                return RestoreResult.Failed(msg);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Restore operation was cancelled.");
            return RestoreResult.Failed("Restore operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database restore failed for file {Path}", backupFilePath);
            return RestoreResult.Failed(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanWriteToPathAsync(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return false;
        }

        return await Task.Run(() =>
        {
            string? probeFile = null;
            try
            {
                if (destinationPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    return false;
                }

                string? dir = null;
                if (Directory.Exists(destinationPath))
                {
                    dir = destinationPath;
                }
                else
                {
                    var fileName = Path.GetFileName(destinationPath);
                    if (!string.IsNullOrEmpty(fileName) && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        return false;
                    }

                    dir = Path.GetDirectoryName(destinationPath);
                    if (string.IsNullOrWhiteSpace(dir))
                    {
                        dir = Directory.GetCurrentDirectory();
                    }
                }

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                probeFile = Path.Combine(dir, $".probe_{Guid.NewGuid():N}.tmp");
                using (var fs = new FileStream(probeFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    fs.WriteByte(1);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Path write permission check failed for {Path}", destinationPath);
                return false;
            }
            finally
            {
                if (probeFile != null && File.Exists(probeFile))
                {
                    try { File.Delete(probeFile); } catch { /* ignore probe cleanup error */ }
                }
            }
        }).ConfigureAwait(false);
    }

    #region SQL Server Implementation

    private string ResolveSqlServerDatabaseName(SupermarketDbContext context)
    {
        var connectionString = GetSqlServerConnectionString(context);

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                return builder.InitialCatalog;
            }
        }
        catch
        {
            // Fall back to database connection name
        }

        var fromConn = context.Database.GetDbConnection().Database;
        if (!string.IsNullOrWhiteSpace(fromConn))
        {
            return fromConn;
        }

        return "SupermarketDb";
    }

    private string GetSqlServerConnectionString(SupermarketDbContext context)
    {
        return _configuration.GetConnectionString("SupermarketDb")
            ?? context.Database.GetDbConnection().ConnectionString;
    }

    private async Task<BackupResult> CreateSqlServerBackupAsync(
        SupermarketDbContext context,
        string destinationFilePath,
        CancellationToken cancellationToken)
    {
        var connectionString = GetSqlServerConnectionString(context);
        var dbName = ResolveSqlServerDatabaseName(context);
        var safeDbName = dbName.Replace("]", "]]");

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        bool directSuccess = false;
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"BACKUP DATABASE [{safeDbName}] TO DISK = @path WITH FORMAT, INIT, CHECKSUM;";
            cmd.CommandTimeout = 300;
            cmd.Parameters.Add(new SqlParameter("@path", destinationFilePath));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            directSuccess = true;
        }
        catch (SqlException sqlEx) when (sqlEx.Number == 3201 || sqlEx.Message.Contains("Operating system error 5", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(sqlEx, "Direct SQL Server backup failed with access restriction. Falling back to staging directory.");
        }

        if (!directSuccess)
        {
            string? stagingDir = null;
            try
            {
                using var cmdProp = conn.CreateCommand();
                cmdProp.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(512));";
                var propVal = await cmdProp.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (propVal != null && propVal != DBNull.Value)
                {
                    stagingDir = propVal.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query InstanceDefaultBackupPath from SQL Server.");
            }

            if (string.IsNullOrWhiteSpace(stagingDir) || !Directory.Exists(stagingDir))
            {
                stagingDir = Path.GetTempPath();
            }

            var stagingFile = Path.Combine(stagingDir, $"BackupStaging_{Guid.NewGuid():N}.bak");
            try
            {
                using (var cmdStage = conn.CreateCommand())
                {
                    cmdStage.CommandText = $"BACKUP DATABASE [{safeDbName}] TO DISK = @path WITH FORMAT, INIT, CHECKSUM;";
                    cmdStage.CommandTimeout = 300;
                    cmdStage.Parameters.Add(new SqlParameter("@path", stagingFile));
                    await cmdStage.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                File.Copy(stagingFile, destinationFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(stagingFile))
                {
                    try { File.Delete(stagingFile); } catch { /* ignore cleanup error */ }
                }
            }
        }

        var fileInfo = new FileInfo(destinationFilePath);
        if (!fileInfo.Exists)
        {
            return BackupResult.Failed("Backup completed without exceptions, but target file was not created.");
        }

        _logger.LogInformation("SQL Server backup created successfully at {Path} ({Bytes} bytes)", destinationFilePath, fileInfo.Length);
        return BackupResult.Succeeded(destinationFilePath, fileInfo.Length, DateTime.UtcNow);
    }

    private async Task<RestoreResult> RestoreSqlServerDatabaseAsync(
        SupermarketDbContext context,
        string backupFilePath,
        CancellationToken cancellationToken)
    {
        var connectionString = GetSqlServerConnectionString(context);
        var dbName = ResolveSqlServerDatabaseName(context);
        var safeDbName = dbName.Replace("]", "]]");

        var masterBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
            Pooling = false
        };

        using var masterConn = new SqlConnection(masterBuilder.ConnectionString);
        await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Step 1: Clear all client connection pools prior to terminating sessions
        SqlConnection.ClearAllPools();

        string? stagingFile = null;
        try
        {
            // Step 2: Set SINGLE_USER WITH ROLLBACK IMMEDIATE to sever client connections
            using (var cmdSingle = masterConn.CreateCommand())
            {
                cmdSingle.CommandText = $"ALTER DATABASE [{safeDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
                cmdSingle.CommandTimeout = 120;
                await cmdSingle.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Step 3: Execute RESTORE DATABASE
            bool directSuccess = false;
            try
            {
                using var cmdRestore = masterConn.CreateCommand();
                cmdRestore.CommandText = $"RESTORE DATABASE [{safeDbName}] FROM DISK = @path WITH REPLACE;";
                cmdRestore.CommandTimeout = 600;
                cmdRestore.Parameters.Add(new SqlParameter("@path", backupFilePath));
                await cmdRestore.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                directSuccess = true;
            }
            catch (SqlException sqlEx) when (sqlEx.Number == 3201 || sqlEx.Message.Contains("Operating system error 5", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(sqlEx, "Direct SQL Server restore failed with access restriction. Falling back to staging directory.");
            }

            if (!directSuccess)
            {
                string? stagingDir = null;
                try
                {
                    using var cmdProp = masterConn.CreateCommand();
                    cmdProp.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(512));";
                    var propVal = await cmdProp.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    if (propVal != null && propVal != DBNull.Value)
                    {
                        stagingDir = propVal.ToString();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query InstanceDefaultBackupPath from SQL Server.");
                }

                if (string.IsNullOrWhiteSpace(stagingDir) || !Directory.Exists(stagingDir))
                {
                    stagingDir = Path.GetTempPath();
                }

                stagingFile = Path.Combine(stagingDir, $"RestoreStaging_{Guid.NewGuid():N}.bak");
                File.Copy(backupFilePath, stagingFile, overwrite: true);

                using var cmdStage = masterConn.CreateCommand();
                cmdStage.CommandText = $"RESTORE DATABASE [{safeDbName}] FROM DISK = @path WITH REPLACE;";
                cmdStage.CommandTimeout = 600;
                cmdStage.Parameters.Add(new SqlParameter("@path", stagingFile));
                await cmdStage.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (stagingFile != null && File.Exists(stagingFile))
            {
                try { File.Delete(stagingFile); } catch { /* ignore cleanup error */ }
            }

            // Step 4: Always revert database to MULTI_USER mode
            try
            {
                using var cmdMulti = masterConn.CreateCommand();
                cmdMulti.CommandText = $"ALTER DATABASE [{safeDbName}] SET MULTI_USER;";
                cmdMulti.CommandTimeout = 120;
                await cmdMulti.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exMulti)
            {
                _logger.LogError(exMulti, "Failed to reset database [{Database}] to MULTI_USER mode.", safeDbName);
            }

            // Step 5: Clear client connection pools again to purge severed session handles
            SqlConnection.ClearAllPools();
        }

        _logger.LogInformation("SQL Server database [{Database}] restored successfully from {Path}", safeDbName, backupFilePath);
        return RestoreResult.Succeeded();
    }

    #endregion

    #region SQLite Implementation (Test Harness & Embedded)

    private void ClearSqlitePools()
    {
        try
        {
            var sqliteType = Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite");
            if (sqliteType != null)
            {
                var clearMethod = sqliteType.GetMethod("ClearAllPools", BindingFlags.Public | BindingFlags.Static);
                clearMethod?.Invoke(null, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invoke SqliteConnection.ClearAllPools via reflection.");
        }
    }

    private async Task<bool> ExecuteSqliteNativeBackupAsync(DbConnection sourceConn, string destinationFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var sqliteType = sourceConn.GetType();
            var backupMethod = sqliteType.GetMethod("BackupDatabase", new[] { sqliteType });
            if (backupMethod == null)
            {
                return false;
            }

            var destConnStr = $"Data Source={destinationFilePath}";
            var destConn = (DbConnection)Activator.CreateInstance(sqliteType, destConnStr)!;
            await using (destConn.ConfigureAwait(false))
            {
                await destConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                backupMethod.Invoke(sourceConn, new object[] { destConn });
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQLite native BackupDatabase invocation failed.");
            return false;
        }
    }

    private async Task<bool> ExecuteSqliteNativeRestoreAsync(DbConnection destConn, string backupFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var sqliteType = destConn.GetType();
            var backupMethod = sqliteType.GetMethod("BackupDatabase", new[] { sqliteType });
            if (backupMethod == null)
            {
                return false;
            }

            var srcConnStr = $"Data Source={backupFilePath}";
            var srcConn = (DbConnection)Activator.CreateInstance(sqliteType, srcConnStr)!;
            await using (srcConn.ConfigureAwait(false))
            {
                await srcConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                // Copy pages from srcConn to destConn
                backupMethod.Invoke(srcConn, new object[] { destConn });
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQLite native restore invocation failed.");
            return false;
        }
    }

    private async Task<BackupResult> CreateSqliteBackupAsync(
        SupermarketDbContext context,
        string destinationFilePath,
        CancellationToken cancellationToken)
    {
        var dbConn = context.Database.GetDbConnection();
        if (dbConn.State != ConnectionState.Open)
        {
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(destinationFilePath))
        {
            File.Delete(destinationFilePath);
        }

        bool backupSucceeded = false;

        // 1. Try VACUUM INTO
        try
        {
            using var cmd = dbConn.CreateCommand();
            cmd.CommandText = $"VACUUM INTO '{destinationFilePath.Replace("'", "''")}';";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            backupSucceeded = File.Exists(destinationFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQLite VACUUM INTO failed; attempting native BackupDatabase API fallback.");
        }

        // 2. Fallback to native BackupDatabase
        if (!backupSucceeded)
        {
            backupSucceeded = await ExecuteSqliteNativeBackupAsync(dbConn, destinationFilePath, cancellationToken).ConfigureAwait(false);
        }

        if (!backupSucceeded || !File.Exists(destinationFilePath))
        {
            return BackupResult.Failed("Failed to create SQLite database backup.");
        }

        var fileInfo = new FileInfo(destinationFilePath);
        _logger.LogInformation("SQLite backup created successfully at {Path} ({Bytes} bytes)", destinationFilePath, fileInfo.Length);
        return BackupResult.Succeeded(destinationFilePath, fileInfo.Length, DateTime.UtcNow);
    }

    private async Task<RestoreResult> RestoreSqliteDatabaseAsync(
        SupermarketDbContext context,
        string backupFilePath,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(backupFilePath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            return RestoreResult.Failed("Backup file is empty.");
        }

        if (fileInfo.Length < 16)
        {
            return RestoreResult.Failed("Backup file is corrupt or not a valid database.");
        }

        byte[] sqliteMagic = new byte[] { 0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00 };
        byte[] header = new byte[16];
        using (var fs = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            int bytesRead = await fs.ReadAsync(header, 0, 16, cancellationToken).ConfigureAwait(false);
            if (bytesRead < 16)
            {
                return RestoreResult.Failed("Backup file is corrupt or not a valid database.");
            }
        }

        if (!header.SequenceEqual(sqliteMagic))
        {
            return RestoreResult.Failed("Backup file is corrupt or not a valid database.");
        }

        ClearSqlitePools();

        var dbConn = context.Database.GetDbConnection();
        if (dbConn.State != ConnectionState.Open)
        {
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        bool restored = await ExecuteSqliteNativeRestoreAsync(dbConn, backupFilePath, cancellationToken).ConfigureAwait(false);
        if (!restored)
        {
            return RestoreResult.Failed("Failed to restore SQLite database from backup file.");
        }

        ClearSqlitePools();

        bool canConnect = await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        if (!canConnect)
        {
            return RestoreResult.Failed("Could not verify database connectivity after restore.");
        }

        _logger.LogInformation("SQLite database restored successfully from {Path}", backupFilePath);
        return RestoreResult.Succeeded();
    }

    #endregion
}
