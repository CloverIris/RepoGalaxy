namespace RepoGalaxy.Core.Interfaces;

public readonly record struct CacheKey(string Value)
{
    public override string ToString() => Value;
    public static CacheKey Create(string prefix, params object?[] parts) =>
        new($"{prefix}:{string.Join(':', parts.Select(p => p?.ToString()?.Trim().ToLowerInvariant() ?? "_"))}");
}

public enum CacheEntryState { Miss, Fresh, Stale }

public sealed record CachePolicy(
    TimeSpan FreshFor,
    TimeSpan StaleFor,
    IReadOnlyCollection<string>? Tags = null)
{
    public static CachePolicy Feed(TimeSpan? freshFor = null) =>
        new(freshFor ?? TimeSpan.FromMinutes(30), TimeSpan.FromDays(7), ["feed"]);
}

public sealed record CacheReadResult<T>(
    CacheEntryState State,
    T? Value,
    DateTimeOffset? FetchedAt = null,
    DateTimeOffset? ExpiresAt = null)
{
    public bool HasValue => State != CacheEntryState.Miss && Value is not null;
    public static CacheReadResult<T> Miss() => new(CacheEntryState.Miss, default);
}

public sealed record CacheStatistics(
    long MemoryBytes,
    long PersistentBytes,
    long Hits,
    long StaleHits,
    long Misses,
    DateTimeOffset? LastPrunedAt)
{
    public long TotalBytes => MemoryBytes + PersistentBytes;
    public double HitRate => Hits + StaleHits + Misses == 0 ? 0 : (double)(Hits + StaleHits) / (Hits + StaleHits + Misses);
}

public interface IMemoryCacheStore
{
    ValueTask<CacheReadResult<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default);
    ValueTask SetAsync<T>(CacheKey key, T value, CachePolicy policy, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
    ValueTask InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
    long SizeBytes { get; }
    CacheStatistics GetStatistics();
    void SetSizeLimit(long bytes);
}

public interface IPersistentCacheStore
{
    Task<CacheReadResult<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(CacheKey key, T value, CachePolicy policy, string? etag = null, string? lastModified = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
    Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);
    Task<long> PruneAsync(long sizeLimitBytes, CancellationToken cancellationToken = default);
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    void SetSizeLimit(long bytes);
}

public interface ICacheService
{
    Task<CacheReadResult<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(CacheKey key, T value, CachePolicy policy, CancellationToken cancellationToken = default);
    Task SetValidatedAsync<T>(CacheKey key, T value, CachePolicy policy, string? etag, string? lastModified, CancellationToken cancellationToken = default);
    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
    Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    Task<long> PruneAsync(long persistentSizeLimitBytes, CancellationToken cancellationToken = default);
    void SetPersistentSizeLimit(long bytes);
}

public interface ILazyRefreshCoordinator
{
    Task<T> GetOrRefreshAsync<T>(CacheKey key, CachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);
    Task RefreshInBackgroundAsync<T>(CacheKey key, CachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);
}
