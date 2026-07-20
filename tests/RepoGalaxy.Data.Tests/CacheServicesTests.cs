using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Data.Services;
using Xunit;

namespace RepoGalaxy.Data.Tests;

public sealed class CacheServicesTests
{
    [Fact]
    public async Task Memory_cache_reports_fresh_and_stale_states()
    {
        var cache = new MemoryCacheStore();
        await cache.SetAsync(CacheKey.Create("test", "fresh"), "value", new CachePolicy(TimeSpan.FromMinutes(1), TimeSpan.FromDays(1)));
        await cache.SetAsync(CacheKey.Create("test", "stale"), "old", new CachePolicy(TimeSpan.Zero, TimeSpan.FromDays(1)));

        (await cache.GetAsync<string>(CacheKey.Create("test", "fresh"))).State.Should().Be(CacheEntryState.Fresh);
        (await cache.GetAsync<string>(CacheKey.Create("test", "stale"))).State.Should().Be(CacheEntryState.Stale);
    }

    [Fact]
    public async Task Memory_cache_evicts_the_least_recently_used_serialized_payload()
    {
        var cache = new MemoryCacheStore();
        cache.SetSizeLimit(7);
        var policy = new CachePolicy(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        await cache.SetAsync(new CacheKey("a"), "a", policy);
        await cache.SetAsync(new CacheKey("b"), "b", policy);
        _ = await cache.GetAsync<string>(new CacheKey("a"));
        await cache.SetAsync(new CacheKey("c"), "c", policy);

        (await cache.GetAsync<string>(new CacheKey("a"))).HasValue.Should().BeTrue();
        (await cache.GetAsync<string>(new CacheKey("b"))).State.Should().Be(CacheEntryState.Miss);
        (await cache.GetAsync<string>(new CacheKey("c"))).HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task Lazy_refresh_is_single_flight_for_the_same_key_and_type()
    {
        await using var database = await TestDatabase.CreateAsync();
        var memory = new MemoryCacheStore();
        var persistent = new PersistentCacheStore(database.Factory);
        var coordinator = new LazyRefreshCoordinator(new LayeredCacheService(memory, persistent));
        var calls = 0;

        var operations = Enumerable.Range(0, 12).Select(_ => coordinator.GetOrRefreshAsync(
            new CacheKey("single-flight"),
            new CachePolicy(TimeSpan.FromMinutes(1), TimeSpan.FromDays(1)),
            async token => { Interlocked.Increment(ref calls); await Task.Delay(40, token); return 42; }));
        var values = await Task.WhenAll(operations);

        values.Should().OnlyContain(x => x == 42);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Persistent_cache_isolates_a_corrupt_payload()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var db = await database.Factory.CreateDbContextAsync())
        {
            db.ApiCacheEntries.Add(new ApiCacheEntryEntity { Key = "broken", Payload = [1, 2, 3], FetchedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1), StaleUntil = DateTimeOffset.UtcNow.AddDays(1), LastAccessedAt = DateTimeOffset.UtcNow, SizeBytes = 3 });
            await db.SaveChangesAsync();
        }
        var cache = new PersistentCacheStore(database.Factory);

        (await cache.GetAsync<string>(new CacheKey("broken"))).State.Should().Be(CacheEntryState.Miss);
        await using var verification = await database.Factory.CreateDbContextAsync();
        (await verification.ApiCacheEntries.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Tag_invalidation_matches_a_complete_tag_only()
    {
        await using var database = await TestDatabase.CreateAsync();
        var cache = new PersistentCacheStore(database.Factory);
        var policy = new CachePolicy(TimeSpan.FromMinutes(1), TimeSpan.FromDays(1), ["private"]);
        await cache.SetAsync(new CacheKey("private"), 1, policy);
        await cache.SetAsync(new CacheKey("public"), 2, policy with { Tags = ["notprivate"] });

        await cache.InvalidateTagAsync("private");

        (await cache.GetAsync<int>(new CacheKey("private"))).State.Should().Be(CacheEntryState.Miss);
        (await cache.GetAsync<int>(new CacheKey("public"))).Value.Should().Be(2);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _path;
        public IDbContextFactory<RepoGalaxyDbContext> Factory { get; }
        private TestDatabase(string path, IDbContextFactory<RepoGalaxyDbContext> factory) { _path = path; Factory = factory; }
        public static async Task<TestDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"repogalaxy-cache-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            IDbContextFactory<RepoGalaxyDbContext> factory = new FactoryAdapter(options);
            await using var db = await factory.CreateDbContextAsync(); await db.Database.EnsureCreatedAsync();
            return new TestDatabase(path, factory);
        }
        public ValueTask DisposeAsync() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(_path)) File.Delete(_path); return ValueTask.CompletedTask; }
        private sealed class FactoryAdapter(DbContextOptions<RepoGalaxyDbContext> options) : IDbContextFactory<RepoGalaxyDbContext>
        {
            public RepoGalaxyDbContext CreateDbContext() => new(options);
        }
    }
}
