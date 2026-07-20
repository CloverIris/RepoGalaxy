using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Services;
using Xunit;

namespace RepoGalaxy.Data.Tests;

public sealed class MetroTileLayoutServiceTests
{
    [Fact]
    public async Task Synchronize_preserves_coordinates_expands_without_overlap_and_restores_viewport()
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
        AssertNoOverlap(expanded.Placements);

        await service.SaveViewportAsync(expanded.Id, 321.5, 82);
        var restored = await service.LoadAsync("GUEST", FeedSource.Trending);
        restored.ViewportX.Should().Be(321.5);
        restored.ViewportY.Should().Be(82);

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
