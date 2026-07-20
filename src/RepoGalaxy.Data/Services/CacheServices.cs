using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

public sealed class MemoryCacheStore : IMemoryCacheStore
{
    private sealed record Entry(byte[] Payload, DateTimeOffset FetchedAt, DateTimeOffset ExpiresAt, DateTimeOffset StaleUntil, HashSet<string> Tags);
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new(StringComparer.Ordinal);
    private long _sizeLimit = 256L * 1024 * 1024;
    private long _hits, _staleHits, _misses;
    public long SizeBytes { get; private set; }

    public ValueTask<CacheReadResult<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_entries.TryGetValue(key.Value, out var entry)) { Interlocked.Increment(ref _misses); return ValueTask.FromResult(CacheReadResult<T>.Miss()); }
            if (entry.StaleUntil <= DateTimeOffset.UtcNow) { RemoveCore(key.Value); Interlocked.Increment(ref _misses); return ValueTask.FromResult(CacheReadResult<T>.Miss()); }
            Touch(key.Value);
            try
            {
                var value = JsonSerializer.Deserialize<T>(entry.Payload);
                var state = entry.ExpiresAt > DateTimeOffset.UtcNow ? CacheEntryState.Fresh : CacheEntryState.Stale;
                if (state == CacheEntryState.Fresh) Interlocked.Increment(ref _hits); else Interlocked.Increment(ref _staleHits);
                return ValueTask.FromResult(new CacheReadResult<T>(state, value, entry.FetchedAt, entry.ExpiresAt));
            }
            catch { RemoveCore(key.Value); Interlocked.Increment(ref _misses); return ValueTask.FromResult(CacheReadResult<T>.Miss()); }
        }
    }

    public ValueTask SetAsync<T>(CacheKey key, T value, CachePolicy policy, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            RemoveCore(key.Value);
            _entries[key.Value] = new Entry(payload, now, now + policy.FreshFor, now + policy.FreshFor + policy.StaleFor, new(policy.Tags ?? [], StringComparer.OrdinalIgnoreCase));
            SizeBytes += payload.LongLength;
            Touch(key.Value);
            Trim();
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(CacheKey key, CancellationToken cancellationToken = default) { lock (_gate) RemoveCore(key.Value); return ValueTask.CompletedTask; }
    public ValueTask InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        lock (_gate) foreach (var key in _entries.Where(x => x.Value.Tags.Contains(tag)).Select(x => x.Key).ToArray()) RemoveCore(key);
        return ValueTask.CompletedTask;
    }
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) { lock (_gate) { _entries.Clear(); _lru.Clear(); _nodes.Clear(); SizeBytes = 0; } return ValueTask.CompletedTask; }
    public void SetSizeLimit(long bytes) { lock (_gate) { _sizeLimit = Math.Max(1, bytes); Trim(); } }
    public CacheStatistics GetStatistics() => new(SizeBytes, 0, Interlocked.Read(ref _hits), Interlocked.Read(ref _staleHits), Interlocked.Read(ref _misses), null);
    private void Trim() { while (SizeBytes > _sizeLimit && _lru.First is { } node) RemoveCore(node.Value); }
    private void Touch(string key) { if (_nodes.Remove(key, out var old)) _lru.Remove(old); _nodes[key] = _lru.AddLast(key); }
    private void RemoveCore(string key) { if (_entries.Remove(key, out var old)) SizeBytes -= old.Payload.LongLength; if (_nodes.Remove(key, out var node)) _lru.Remove(node); }
}

public sealed class PersistentCacheStore : IPersistentCacheStore
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private long _hits, _staleHits, _misses;
    private DateTimeOffset? _lastPrunedAt;
    private long _sizeLimit = 1024L * 1024 * 1024;
    public PersistentCacheStore(IDbContextFactory<RepoGalaxyDbContext> factory) => _factory = factory;

    public async Task<CacheReadResult<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var item = await db.ApiCacheEntries.FirstOrDefaultAsync(x => x.Key == key.Value, cancellationToken);
        if (item is null || item.StaleUntil <= DateTimeOffset.UtcNow) { Interlocked.Increment(ref _misses); if (item is not null) { db.Remove(item); await db.SaveChangesWithRetryAsync(cancellationToken); } return CacheReadResult<T>.Miss(); }
        try
        {
            item.LastAccessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesWithRetryAsync(cancellationToken);
            var value = JsonSerializer.Deserialize<T>(Decompress(item.Payload));
            var state = item.ExpiresAt > DateTimeOffset.UtcNow ? CacheEntryState.Fresh : CacheEntryState.Stale;
            if (state == CacheEntryState.Fresh) Interlocked.Increment(ref _hits); else Interlocked.Increment(ref _staleHits);
            return new(state, value, item.FetchedAt, item.ExpiresAt);
        }
        catch { db.Remove(item); await db.SaveChangesWithRetryAsync(cancellationToken); Interlocked.Increment(ref _misses); return CacheReadResult<T>.Miss(); }
    }

    public async Task SetAsync<T>(CacheKey key, T value, CachePolicy policy, string? etag = null, string? lastModified = null, CancellationToken cancellationToken = default)
    {
        var payload = Compress(JsonSerializer.SerializeToUtf8Bytes(value));
        var now = DateTimeOffset.UtcNow;
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var item = await db.ApiCacheEntries.FindAsync([key.Value], cancellationToken) ?? new ApiCacheEntryEntity { Key = key.Value };
        item.Payload = payload; item.ETag = etag; item.LastModified = lastModified; item.Tags = "|" + string.Join('|', (policy.Tags ?? []).Select(x => x.Trim().ToLowerInvariant())) + "|";
        item.FetchedAt = now; item.ExpiresAt = now + policy.FreshFor; item.StaleUntil = now + policy.FreshFor + policy.StaleFor; item.LastAccessedAt = now; item.SizeBytes = payload.LongLength;
        if (db.Entry(item).State == EntityState.Detached) db.Add(item);
        await db.SaveChangesWithRetryAsync(cancellationToken);
        await PruneAsync(_sizeLimit, cancellationToken);
    }

    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default) { await using var db = await _factory.CreateDbContextAsync(cancellationToken); await db.ApiCacheEntries.Where(x => x.Key == key.Value).ExecuteDeleteAsync(cancellationToken); }
    public async Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default) { await using var db = await _factory.CreateDbContextAsync(cancellationToken); var marker = $"%|{tag.Trim().ToLowerInvariant()}|%"; await db.ApiCacheEntries.Where(x => EF.Functions.Like(x.Tags, marker)).ExecuteDeleteAsync(cancellationToken); }
    public async Task<long> PruneAsync(long sizeLimitBytes, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await db.ApiCacheEntries.Where(x => x.StaleUntil <= now).ExecuteDeleteAsync(cancellationToken);
        var rows = await db.ApiCacheEntries.OrderByDescending(x => x.LastAccessedAt).ToListAsync(cancellationToken);
        long kept = 0, removed = 0;
        foreach (var row in rows) { if (kept + row.SizeBytes <= sizeLimitBytes) kept += row.SizeBytes; else { removed += row.SizeBytes; db.Remove(row); } }
        await db.SaveChangesWithRetryAsync(cancellationToken); _lastPrunedAt = now; return removed;
    }
    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default) { await using var db = await _factory.CreateDbContextAsync(cancellationToken); var bytes = await db.ApiCacheEntries.SumAsync(x => (long?)x.SizeBytes, cancellationToken) ?? 0; return new(0, bytes, Interlocked.Read(ref _hits), Interlocked.Read(ref _staleHits), Interlocked.Read(ref _misses), _lastPrunedAt); }
    public void SetSizeLimit(long bytes) => Interlocked.Exchange(ref _sizeLimit, Math.Max(1, bytes));
    private static byte[] Compress(byte[] source) { using var output = new MemoryStream(); using (var gzip = new GZipStream(output, CompressionLevel.Fastest, true)) gzip.Write(source); return output.ToArray(); }
    private static byte[] Decompress(byte[] source) { using var input = new MemoryStream(source); using var gzip = new GZipStream(input, CompressionMode.Decompress); using var output = new MemoryStream(); gzip.CopyTo(output); return output.ToArray(); }
}

public sealed class LayeredCacheService : ICacheService
{
    private readonly IMemoryCacheStore _memory; private readonly IPersistentCacheStore _persistent;
    public LayeredCacheService(IMemoryCacheStore memory, IPersistentCacheStore persistent) { _memory = memory; _persistent = persistent; }
    public async Task<CacheReadResult<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default) { var hot = await _memory.GetAsync<T>(key, cancellationToken); if (hot.HasValue) return hot; var stored = await _persistent.GetAsync<T>(key, cancellationToken); if (stored.HasValue) { var remaining = (stored.ExpiresAt ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow; await _memory.SetAsync(key, stored.Value!, new CachePolicy(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, TimeSpan.FromDays(7)), cancellationToken); } return stored; }
    public async Task SetAsync<T>(CacheKey key, T value, CachePolicy policy, CancellationToken cancellationToken = default) { await _persistent.SetAsync(key, value, policy, cancellationToken: cancellationToken); await _memory.SetAsync(key, value, policy, cancellationToken); }
    public async Task SetValidatedAsync<T>(CacheKey key, T value, CachePolicy policy, string? etag, string? lastModified, CancellationToken cancellationToken = default) { await _persistent.SetAsync(key, value, policy, etag, lastModified, cancellationToken); await _memory.SetAsync(key, value, policy, cancellationToken); }
    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default) { await _memory.RemoveAsync(key, cancellationToken); await _persistent.RemoveAsync(key, cancellationToken); }
    public async Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default) { await _memory.InvalidateTagAsync(tag, cancellationToken); await _persistent.InvalidateTagAsync(tag, cancellationToken); }
    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default) { var persistent = await _persistent.GetStatisticsAsync(cancellationToken); var memory = _memory.GetStatistics(); return persistent with { MemoryBytes = memory.MemoryBytes, Hits = persistent.Hits + memory.Hits, StaleHits = persistent.StaleHits + memory.StaleHits }; }
    public Task<long> PruneAsync(long persistentSizeLimitBytes, CancellationToken cancellationToken = default) => _persistent.PruneAsync(persistentSizeLimitBytes, cancellationToken);
    public void SetPersistentSizeLimit(long bytes) => _persistent.SetSizeLimit(bytes);
}

public sealed class LazyRefreshCoordinator : ILazyRefreshCoordinator
{
    private readonly ICacheService _cache;
    private readonly ConcurrentDictionary<string, Task> _inflight = new(StringComparer.Ordinal);
    public LazyRefreshCoordinator(ICacheService cache) => _cache = cache;
    public async Task<T> GetOrRefreshAsync<T>(CacheKey key, CachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<T>(key, cancellationToken);
        if (cached.State == CacheEntryState.Fresh && cached.Value is not null) return cached.Value;
        if (cached.State == CacheEntryState.Stale && cached.Value is not null) { _ = RefreshIgnoringFailureAsync(key, policy, factory, cancellationToken); return cached.Value; }
        return await RefreshCoreAsync(key, policy, factory, cancellationToken);
    }
    public Task RefreshInBackgroundAsync<T>(CacheKey key, CachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default) => RefreshCoreAsync(key, policy, factory, cancellationToken);
    private async Task<T> RefreshCoreAsync<T>(CacheKey key, CachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
    {
        Task<T> Create() => ExecuteAsync();
        async Task<T> ExecuteAsync() { var value = await factory(cancellationToken); await _cache.SetAsync(key, value, policy, cancellationToken); return value; }
        var flightKey = $"{typeof(T).AssemblyQualifiedName}|{key.Value}";
        var task = (Task<T>)_inflight.GetOrAdd(flightKey, _ => Create());
        try { return await task; } finally { _inflight.TryRemove(new KeyValuePair<string, Task>(flightKey, task)); }
    }
    private async Task RefreshIgnoringFailureAsync<T>(CacheKey key, CachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
    {
        try { await RefreshCoreAsync(key, policy, factory, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch { }
    }
}
