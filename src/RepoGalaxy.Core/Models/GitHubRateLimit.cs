namespace RepoGalaxy.Core.Models;

public sealed class GitHubRateLimit
{
    public int CoreRemaining { get; init; }
    public int CoreLimit { get; init; }
    public DateTimeOffset CoreResetAt { get; init; }
    public int SearchRemaining { get; init; }
    public int SearchLimit { get; init; }
    public DateTimeOffset SearchResetAt { get; init; }
    public bool IsExhausted => CoreRemaining <= 0 || SearchRemaining <= 0;
}

public sealed record GitHubResponse<T>(T? Data, int StatusCode, string Resource, GitHubRateWindow? RateLimit, string? ETag = null, string? LastModified = null, bool NotModified = false, string? NextPageUrl = null);
public sealed record GitHubPage<T>(IReadOnlyList<T> Items, string? NextPageUrl, GitHubRateWindow? RateLimit, string? ETag = null, string? LastModified = null);
public sealed record GitHubRateWindow(string Resource, int Limit, int Remaining, DateTimeOffset ResetAt);
