using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
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
