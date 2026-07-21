using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Data.Services;
using Xunit;

namespace RepoGalaxy.Data.Tests;

public sealed class MetroTileLayoutServiceTests
{
    [Fact]
    public async Task Three_scale_migration_discards_only_v1_tile_layout_data()
    {
        var path = Path.Combine(Path.GetTempPath(), $"repogalaxy-migration-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var db = new RepoGalaxyDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260720150154_MetroTileLayout");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO GitIdentityAliases (Name, Email, IsEnabled) VALUES ('developer', 'dev@example.com', 1);");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO TileBoards (ScopeKey, Source, LayoutVersion, ViewportX, ViewportY, ExtentColumns, ExtentRows, UpdatedAt) VALUES ('guest', 0, 1, 120, 80, 18, 6, 1);");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO TilePlacements (BoardId, ContentKind, ContentKey, Column, Row, ColumnSpan, RowSpan, Title, Subtitle, Caption, AccentKey, ImageUrl, IsPlaceholder, UpdatedAt) VALUES (1, 'Tip', 'tip:old', 0, 0, 1, 1, 'old', '', '', '', '', 1, 1);");

            await migrator.MigrateAsync();

            await using var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var preserved = connection.CreateCommand();
            preserved.CommandText = "SELECT COUNT(*) FROM GitIdentityAliases;";
            Convert.ToInt32(await preserved.ExecuteScalarAsync()).Should().Be(1);
            await using var discarded = connection.CreateCommand();
            discarded.CommandText = "SELECT COUNT(*) FROM TileBoards;";
            Convert.ToInt32(await discarded.ExecuteScalarAsync()).Should().Be(0);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Synchronize_preserves_coordinates_expands_without_overlap_and_restores_camera()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MetroTileLayoutService(database.Factory);
        var initial = new[]
        {
            new TileContent("lang:csharp", MetroTileKind.Language, "C#", AccentKey: "C#"),
            new TileContent("stack:dotnet", MetroTileKind.Technology, ".NET", AccentKey: ".NET"),
            new TileContent("repo:1", MetroTileKind.Repository, "owner/repo", RepositoryId: null),
            new TileContent("tip:wide:git", MetroTileKind.Tip, "Git", IsPlaceholder: true)
        };

        var first = await service.SynchronizeAsync("guest", FeedSource.Trending, initial, 12, 6);
        var original = first.Placements.ToDictionary(x => x.Content.Key, x => (x.Column, x.Row));
        AssertNoOverlap(first.Placements);

        var expandedContent = initial.Concat(Enumerable.Range(0, 16)
            .Select(x => new TileContent($"language:{x}", MetroTileKind.Language, $"Language {x}"))).ToList();
        var expanded = await service.SynchronizeAsync("guest", FeedSource.Trending, expandedContent, 18, 8);

        foreach (var entry in original)
        {
            var placement = expanded.Placements.Single(x => x.Content.Key == entry.Key);
            placement.Column.Should().Be(entry.Value.Column);
            placement.Row.Should().Be(entry.Value.Row);
        }
        expanded.ExtentColumns.Should().BeGreaterThanOrEqualTo(18);
        expanded.ExtentRows.Should().BeGreaterThanOrEqualTo(8);
        expanded.Placements.Select(x => x.Row).Distinct().Count().Should().BeGreaterThan(1);
        expanded.Placements.Select(x => x.Column).Distinct().Count().Should().BeGreaterThan(1);
        AssertNoOverlap(expanded.Placements);

        await service.SaveCameraAsync(expanded.Id, new CameraState(321.5, 82, .72, "repo:1", SemanticIndexKind.Language, "C#"));
        var restored = await service.LoadAsync("GUEST", FeedSource.Trending);
        restored.CameraX.Should().Be(321.5);
        restored.CameraY.Should().Be(82);
        restored.Zoom.Should().Be(.72);
        restored.ActiveIndexKind.Should().Be(SemanticIndexKind.Language);
        restored.ActiveIndexKey.Should().Be("C#");

        await service.SaveCameraAsync(expanded.Id, new CameraState(1, 2, .22));
        await service.SaveSemanticViewportAsync(expanded.Id, new SemanticViewportState(-180, 24));
        var normalized = await service.LoadAsync("guest", FeedSource.Trending);
        normalized.Zoom.Should().Be(.55);
        normalized.SemanticViewportX.Should().Be(-180);
        normalized.SemanticViewportY.Should().Be(24);

        await service.ResetAsync("guest");
        (await service.LoadAsync("guest", FeedSource.Trending)).Placements.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_content_is_replaced_in_its_existing_slot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MetroTileLayoutService(database.Factory);
        var first = await service.SynchronizeAsync("account", FeedSource.ForYou,
            [new TileContent("repo:gone", MetroTileKind.Repository, "gone")], 12, 6);
        var position = first.Placements.Single();

        var next = await service.SynchronizeAsync("account", FeedSource.ForYou,
            [new TileContent("repo:new", MetroTileKind.Repository, "new")], 12, 6);
        var replacement = next.Placements.Single(x => x.Content.Key == "repo:new");

        (replacement.Column, replacement.Row, replacement.ColumnSpan, replacement.RowSpan)
            .Should().Be((position.Column, position.Row, position.ColumnSpan, position.RowSpan));
    }

    [Fact]
    public async Task Explicit_reflow_rebuilds_every_real_slot_from_the_latest_feed_order()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MetroTileLayoutService(database.Factory);
        var initial = Enumerable.Range(0, 5)
            .Select(index => new TileContent($"repository:old-{index}", MetroTileKind.Repository, $"old/{index}"))
            .ToList();
        await service.SynchronizeAsync("guest", FeedSource.Trending, initial, 18, 10);

        var latest = Enumerable.Range(0, 14)
            .Select(index => new TileContent($"repository:new-{index}", MetroTileKind.Repository, $"new/{index}"))
            .ToList();
        var reflowed = await service.SynchronizeAsync("guest", FeedSource.Trending, latest, 18, 10, reflow: true);

        reflowed.Placements.Where(x => !x.Content.IsPlaceholder).Select(x => x.Content.Key)
            .Should().BeEquivalentTo(latest.Select(x => x.Key));
        reflowed.Placements.Should().OnlyContain(x => !x.Content.Key.StartsWith("repository:old-", StringComparison.Ordinal));
        AssertNoOverlap(reflowed.Placements);
    }

    [Fact]
    public async Task V3_layout_uses_a_stable_compact_sixteen_by_ten_data_island()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MetroTileLayoutService(database.Factory);
        var content = Enumerable.Range(0, 180)
            .Select(index => new TileContent($"repository:{index}", MetroTileKind.Repository, $"owner/repository-{index}"))
            .Concat(Enumerable.Range(0, 20)
                .Select(index => new TileContent($"language:{index}", MetroTileKind.Language, $"Language {index}")))
            .ToList();

        var board = await service.SynchronizeAsync("guest", FeedSource.Trending, content, 18, 10);

        board.LayoutVersion.Should().Be(3);
        board.Placements.Should().HaveCount(content.Count);
        AssertNoOverlap(board.Placements);
        (board.ExtentColumns / (double)board.ExtentRows).Should().BeInRange(1.25, 2.05);
        board.Placements.Min(x => x.Column).Should().Be(0);
        board.Placements.Min(x => x.Row).Should().Be(0);
    }

    [Fact]
    public async Task Synchronizing_v3_removes_only_obsolete_layout_versions()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var db = await database.Factory.CreateDbContextAsync())
        {
            db.TileBoards.Add(new TileBoardEntity
            {
                ScopeKey = "guest",
                Source = (int)FeedSource.Trending,
                LayoutVersion = 2,
                ExtentColumns = 18,
                ExtentRows = 10,
                WorldSeed = "old"
            });
            await db.SaveChangesAsync();
        }

        var service = new MetroTileLayoutService(database.Factory);
        var board = await service.SynchronizeAsync("guest", FeedSource.Trending,
            [new TileContent("repository:new", MetroTileKind.Repository, "owner/new")], 18, 10);

        board.LayoutVersion.Should().Be(3);
        await using var verify = await database.Factory.CreateDbContextAsync();
        (await verify.TileBoards.CountAsync(x => x.ScopeKey == "guest" && x.Source == (int)FeedSource.Trending && x.LayoutVersion != 3))
            .Should().Be(0);
    }

    [Fact]
    public async Task Reorder_persists_ranked_repository_content_without_moving_geometry()
    {
        await using var database = await TestDatabase.CreateAsync();
        long firstId;
        long secondId;
        await using (var db = await database.Factory.CreateDbContextAsync())
        {
            var first = new RepositoryEntity { GitHubId = "11", Owner = "one", Name = "first" };
            var second = new RepositoryEntity { GitHubId = "22", Owner = "two", Name = "second" };
            db.Repositories.AddRange(first, second);
            await db.SaveChangesAsync();
            firstId = first.Id;
            secondId = second.Id;
        }

        var service = new MetroTileLayoutService(database.Factory);
        var board = await service.SynchronizeAsync("guest", FeedSource.ForYou,
        [
            new TileContent("repository:first", MetroTileKind.Repository, "one/first", RepositoryId: firstId),
            new TileContent("repository:second", MetroTileKind.Repository, "two/second", RepositoryId: secondId)
        ], 12, 8);
        var geometry = board.Placements.Select(x => (x.Column, x.Row, x.ColumnSpan, x.RowSpan)).Order().ToArray();
        var secondSlot = board.Placements.Single(x => x.Content.RepositoryId == secondId);
        var focus = new TileWorldWindow(secondSlot.Column * 100, secondSlot.Row * 100, 596, 96);

        var reordered = await service.ReorderRepositoriesAsync(board.Id, [firstId, secondId], focus);
        var restored = await service.LoadAsync("guest", FeedSource.ForYou);

        reordered.Placements.Select(x => (x.Column, x.Row, x.ColumnSpan, x.RowSpan)).Order().Should().Equal(geometry);
        restored.Placements.OrderBy(x => Math.Pow(x.Column * 100 + 298 - focus.CenterX, 2) + Math.Pow(x.Row * 100 + 48 - focus.CenterY, 2))
            .First().Content.RepositoryId.Should().Be(firstId);
    }

    private static void AssertNoOverlap(IEnumerable<TilePlacement> placements)
    {
        var occupied = new HashSet<(int X, int Y)>();
        foreach (var tile in placements)
            for (var x = tile.Column; x < tile.Column + tile.ColumnSpan; x++)
                for (var y = tile.Row; y < tile.Row + tile.RowSpan; y++)
                    occupied.Add((x, y)).Should().BeTrue($"tile {tile.Content.Key} must not overlap another tile");
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _path;
        public IDbContextFactory<RepoGalaxyDbContext> Factory { get; }
        private TestDatabase(string path, IDbContextFactory<RepoGalaxyDbContext> factory) { _path = path; Factory = factory; }
        public static async Task<TestDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"repogalaxy-tiles-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            IDbContextFactory<RepoGalaxyDbContext> factory = new FactoryAdapter(options);
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();
            return new(path, factory);
        }
        public ValueTask DisposeAsync() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(_path)) File.Delete(_path); return ValueTask.CompletedTask; }
        private sealed class FactoryAdapter(DbContextOptions<RepoGalaxyDbContext> options) : IDbContextFactory<RepoGalaxyDbContext>
        {
            public RepoGalaxyDbContext CreateDbContext() => new(options);
        }
    }
}
