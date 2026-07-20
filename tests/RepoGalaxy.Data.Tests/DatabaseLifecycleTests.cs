using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Services;
using Xunit;

namespace RepoGalaxy.Data.Tests;

public sealed class DatabaseLifecycleTests
{
    [Fact]
    public async Task Initial_migration_uses_integer_utc_instants_and_enables_wal()
    {
        await using var fixture = new DatabaseFixture();
        var result = await fixture.Lifecycle.InitializeAsync();

        result.Success.Should().BeTrue(result.Message);
        await using var connection = new SqliteConnection($"Data Source={fixture.DatabasePath}");
        await connection.OpenAsync();
        await using var schema = connection.CreateCommand(); schema.CommandText = "SELECT type FROM pragma_table_info('Repositories') WHERE name='CreatedAt'";
        ((string?)await schema.ExecuteScalarAsync()).Should().Be("INTEGER");
        await using var journal = connection.CreateCommand(); journal.CommandText = "PRAGMA journal_mode;";
        ((string?)await journal.ExecuteScalarAsync()).Should().Be("wal");
        fixture.Lifecycle.MarkCleanShutdown();
    }

    [Fact]
    public async Task Daily_backup_can_restore_the_database_to_its_last_good_state()
    {
        await using var fixture = new DatabaseFixture();
        (await fixture.Lifecycle.InitializeAsync()).Success.Should().BeTrue();
        await using (var db = await fixture.Factory.CreateDbContextAsync())
        {
            db.DiscoverySubscriptions.Add(new() { Name = "created-after-backup" });
            await db.SaveChangesAsync();
        }

        (await fixture.Lifecycle.RestoreLatestBackupAsync()).Should().BeTrue();
        await using var restored = await fixture.Factory.CreateDbContextAsync();
        (await restored.DiscoverySubscriptions.AnyAsync()).Should().BeFalse();
        fixture.Lifecycle.MarkCleanShutdown();
    }

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"repogalaxy-db-{Guid.NewGuid():N}");
        public string DatabasePath { get; }
        public IDbContextFactory<RepoGalaxyDbContext> Factory { get; }
        public DatabaseLifecycleService Lifecycle { get; }
        public DatabaseFixture()
        {
            Directory.CreateDirectory(_directory); DatabasePath = Path.Combine(_directory, "repogalaxy-v3.db");
            var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite($"Data Source={DatabasePath};Pooling=False;Foreign Keys=True").Options;
            Factory = new FactoryAdapter(options); Lifecycle = new DatabaseLifecycleService(Factory, DatabasePath);
        }
        public ValueTask DisposeAsync() { Lifecycle.MarkCleanShutdown(); SqliteConnection.ClearAllPools(); try { Directory.Delete(_directory, true); } catch { } return ValueTask.CompletedTask; }
        private sealed class FactoryAdapter(DbContextOptions<RepoGalaxyDbContext> options) : IDbContextFactory<RepoGalaxyDbContext> { public RepoGalaxyDbContext CreateDbContext() => new(options); }
    }
}
