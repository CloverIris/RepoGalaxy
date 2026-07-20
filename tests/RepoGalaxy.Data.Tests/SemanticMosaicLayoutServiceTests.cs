using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Services;
using Xunit;

namespace RepoGalaxy.Data.Tests;

public sealed class SemanticMosaicLayoutServiceTests
{
    [Fact]
    public async Task Mixed_mosaic_is_compact_non_overlapping_and_stable()
    {
        await using var database = await TestDatabase.CreateAsync();
        var board = await new MetroTileLayoutService(database.Factory).SynchronizeAsync("guest", FeedSource.Trending, [new("tip:1", MetroTileKind.Tip, "Tip")], 12, 8);
        var service = new SemanticMosaicLayoutService(database.Factory);
        var initial = Enumerable.Range(0, 8).Select(i => Item(SemanticIndexKind.Language, $"Language {i}", 100 - i))
            .Concat(Enumerable.Range(0, 10).Select(i => Item(SemanticIndexKind.Framework, $"Framework {i}", 80 - i))).ToList();

        var first = await service.SynchronizeAsync(board.Id, initial, 1.6);
        first.LayoutVersion.Should().Be(2);
        AssertNoOverlap(first.Placements);
        first.Placements.Where(x => x.Item.Kind == SemanticIndexKind.Language).OrderByDescending(x => x.Item.ProjectCount).Take(4).Should().OnlyContain(x => x.ColumnSpan == 2 && x.RowSpan == 2);
        first.Placements.Where(x => x.Item.Kind == SemanticIndexKind.Framework).OrderByDescending(x => x.Item.ProjectCount).Skip(4).Should().OnlyContain(x => x.ColumnSpan == 2 && x.RowSpan == 1);
        var coordinates = first.Placements.ToDictionary(x => x.Item.Key, x => (x.Column, x.Row, x.ColumnSpan, x.RowSpan));

        var second = await service.SynchronizeAsync(board.Id, initial.Concat([Item(SemanticIndexKind.Language, "Zig", 1)]).ToList(), 1.6);
        AssertNoOverlap(second.Placements);
        foreach (var entry in coordinates)
        {
            var placement = second.Placements.Single(x => x.Item.Key == entry.Key);
            placement.Column.Should().Be(entry.Value.Column);
            placement.Row.Should().Be(entry.Value.Row);
            placement.ColumnSpan.Should().Be(entry.Value.ColumnSpan);
            placement.RowSpan.Should().Be(entry.Value.RowSpan);
        }
    }

    [Fact]
    public async Task Ide_preference_is_scoped_by_account_and_technology()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new IdePreferenceService(database.Factory);
        await service.SetPreferredIdeAsync("Alice", "C#", "vs:18");
        await service.SetPreferredIdeAsync("Alice", "Rust", "rustrover");

        (await service.GetPreferredIdeAsync("alice", "c#")).Should().Be("vs:18");
        (await service.GetPreferredIdeAsync("alice", "rust")).Should().Be("rustrover");
        (await service.GetPreferredIdeAsync("bob", "c#")).Should().BeNull();
    }

    private static SemanticIndexItem Item(SemanticIndexKind kind, string title, int count) => new($"{kind}:{title.ToLowerInvariant()}", title, kind, count, title, [$"repo:{title}"]);
    private static void AssertNoOverlap(IEnumerable<SemanticMosaicPlacement> placements)
    {
        var occupied = new HashSet<(int X, int Y)>();
        foreach (var item in placements)
            for (var x = item.Column; x < item.Column + item.ColumnSpan; x++)
                for (var y = item.Row; y < item.Row + item.RowSpan; y++) occupied.Add((x, y)).Should().BeTrue($"{item.Item.Key} must not overlap another item");
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _path;
        public IDbContextFactory<RepoGalaxyDbContext> Factory { get; }
        private TestDatabase(string path, IDbContextFactory<RepoGalaxyDbContext> factory) { _path = path; Factory = factory; }
        public static async Task<TestDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"repogalaxy-mosaic-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            IDbContextFactory<RepoGalaxyDbContext> factory = new FactoryAdapter(options);
            await using var db = await factory.CreateDbContextAsync(); await db.Database.EnsureCreatedAsync();
            return new(path, factory);
        }
        public ValueTask DisposeAsync() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(_path)) File.Delete(_path); return ValueTask.CompletedTask; }
        private sealed class FactoryAdapter(DbContextOptions<RepoGalaxyDbContext> options) : IDbContextFactory<RepoGalaxyDbContext> { public RepoGalaxyDbContext CreateDbContext() => new(options); }
    }
}
