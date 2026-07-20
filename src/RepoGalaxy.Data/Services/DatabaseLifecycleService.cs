using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Data.DbContexts;

namespace RepoGalaxy.Data.Services;

public sealed record DatabaseInitializationResult(bool Success, string Message, string? BackupPath = null);

public sealed class DatabaseLifecycleService
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly string _databasePath;
    private readonly string _appDirectory;
    private readonly string _markerPath;
    private readonly string _migrationLockPath;
    public DatabaseLifecycleService(IDbContextFactory<RepoGalaxyDbContext> factory, string databasePath)
    {
        _factory = factory; _databasePath = Path.GetFullPath(databasePath); _appDirectory = Path.GetDirectoryName(_databasePath)!; _markerPath = Path.Combine(_appDirectory, "database-running.marker"); _migrationLockPath = Path.Combine(_appDirectory, "database-migration.lock");
    }

    public async Task<DatabaseInitializationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_appDirectory);
            await using var migrationLock = await AcquireMigrationLockAsync(cancellationToken);
            ArchiveLegacyDatabase();
            var uncleanShutdown = File.Exists(_markerPath);
            var databaseExisted = File.Exists(_databasePath);
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            string? migrationBackup = null;
            if (databaseExisted && (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                migrationBackup = await CreateBackupAsync($"pre-migration-{DateTime.UtcNow:yyyyMMdd-HHmmss}", cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=FULL;", cancellationToken);
            if (uncleanShutdown && !await IntegrityCheckAsync(cancellationToken)) return new(false, "数据库完整性检查失败，请从最近备份恢复。", GetLatestBackupPath());
            var backup = await CreateDailyBackupAsync(cancellationToken);
            File.WriteAllText(_markerPath, DateTimeOffset.UtcNow.ToString("O"));
            CleanupBackups();
            return new(true, "数据库已就绪", migrationBackup ?? backup);
        }
        catch (Exception ex) { return new(false, $"数据库初始化失败：{ex.Message}"); }
    }

    public void MarkCleanShutdown() { try { if (File.Exists(_markerPath)) File.Delete(_markerPath); } catch { } }
    public async Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default) { await using var connection = new SqliteConnection($"Data Source={_databasePath};Mode=ReadWrite"); await connection.OpenAsync(cancellationToken); await using var command = connection.CreateCommand(); command.CommandText = "PRAGMA quick_check;"; return string.Equals((string?)await command.ExecuteScalarAsync(cancellationToken), "ok", StringComparison.OrdinalIgnoreCase); }
    public async Task<string?> CreateDailyBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_databasePath)) return null;
        return await CreateBackupAsync(DateTime.UtcNow.ToString("yyyyMMdd"), cancellationToken, reuseExisting: true);
    }
    public string? GetLatestBackupPath() => Directory.Exists(Path.Combine(_appDirectory, "Backups"))
        ? Directory.GetFiles(Path.Combine(_appDirectory, "Backups"), "repogalaxy-v3-*.db").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
        : null;
    public async Task<bool> RestoreLatestBackupAsync(CancellationToken cancellationToken = default)
    {
        var backup = GetLatestBackupPath();
        if (backup is null || !await CheckFileIntegrityAsync(backup, cancellationToken)) return false;
        await using var migrationLock = await AcquireMigrationLockAsync(cancellationToken);
        SqliteConnection.ClearAllPools();
        await using (var source = new SqliteConnection($"Data Source={backup};Mode=ReadOnly"))
        await using (var destination = new SqliteConnection($"Data Source={_databasePath};Mode=ReadWriteCreate"))
        {
            await source.OpenAsync(cancellationToken); await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
        }
        return await CheckFileIntegrityAsync(_databasePath, cancellationToken);
    }
    private async Task<string> CreateBackupAsync(string suffix, CancellationToken cancellationToken, bool reuseExisting = false)
    {
        var directory = Path.Combine(_appDirectory, "Backups"); Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, $"repogalaxy-v3-{suffix}.db");
        if (reuseExisting && File.Exists(target)) return target;
        await using var source = new SqliteConnection($"Data Source={_databasePath};Mode=ReadWrite"); await using var destination = new SqliteConnection($"Data Source={target};Mode=ReadWriteCreate");
        await source.OpenAsync(cancellationToken); await destination.OpenAsync(cancellationToken); source.BackupDatabase(destination); return target;
    }
    private static async Task<bool> CheckFileIntegrityAsync(string path, CancellationToken cancellationToken)
    {
        try { await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly"); await connection.OpenAsync(cancellationToken); await using var command = connection.CreateCommand(); command.CommandText = "PRAGMA quick_check;"; return string.Equals((string?)await command.ExecuteScalarAsync(cancellationToken), "ok", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    private async Task<FileStream> AcquireMigrationLockAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(_migrationLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous); }
            catch (IOException) when (attempt < 19) { await Task.Delay(250, cancellationToken); }
        }
        throw new IOException("另一个 RepoGalaxy 实例正在升级数据库，请稍后重试。");
    }
    private void ArchiveLegacyDatabase()
    {
        if (File.Exists(_databasePath)) return;
        var legacy = Path.Combine(_appDirectory, "repogalaxy-v2.db"); if (!File.Exists(legacy)) return;
        var destination = Path.Combine(_appDirectory, "Backups", "Legacy", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(destination);
        foreach (var suffix in new[] { "", "-wal", "-shm" }) { var source = legacy + suffix; if (File.Exists(source)) File.Move(source, Path.Combine(destination, Path.GetFileName(source))); }
    }
    private void CleanupBackups()
    {
        var directory = Path.Combine(_appDirectory, "Backups"); if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.GetFiles(directory, "repogalaxy-v3-*.db").OrderByDescending(x => x).Skip(7)) { try { File.Delete(file); } catch { } }
        var legacy = Path.Combine(directory, "Legacy"); if (Directory.Exists(legacy)) foreach (var item in Directory.GetDirectories(legacy)) if (Directory.GetCreationTimeUtc(item) < DateTime.UtcNow.AddDays(-7)) { try { Directory.Delete(item, true); } catch { } }
    }
}
