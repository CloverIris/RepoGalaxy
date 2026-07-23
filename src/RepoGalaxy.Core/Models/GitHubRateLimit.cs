namespace RepoGalaxy.Core.Models;

public sealed class GitHubRateLimit
{
    public int CoreRemaining { get; init; }
    public int CoreLimit { get; init; }
    public DateTimeOffset CoreResetAt { get; init; }
    public int SearchRemaining { get; init; }
    public int SearchLimit { get; init; }
    public DateTimeOffset SearchResetAt { get; init; }
    public int CoreUsed { get; init; } = -1;
    public int SearchUsed { get; init; } = -1;
    public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsExhausted => CoreRemaining <= 0 || SearchRemaining <= 0;
}

public sealed record GitHubResponse<T>(T? Data, int StatusCode, string Resource, GitHubRateWindow? RateLimit, string? ETag = null, string? LastModified = null, bool NotModified = false, string? NextPageUrl = null);
public sealed record GitHubPage<T>(IReadOnlyList<T> Items, string? NextPageUrl, GitHubRateWindow? RateLimit, string? ETag = null, string? LastModified = null);
public sealed record GitHubRateWindow(
    string Resource,
    int Limit,
    int Remaining,
    DateTimeOffset ResetAt,
    int Used = -1,
    DateTimeOffset? ObservedAt = null,
    DateTimeOffset? RetryAfter = null)
{
    public int EffectiveUsed => Used >= 0 ? Used : Math.Max(0, Limit - Remaining);
    public double UsedRatio => Limit <= 0 ? 0 : Math.Clamp(EffectiveUsed / (double)Limit, 0, 1);
}

public enum GitHubBudgetSessionKind { Guest, Authenticating, Authenticated }

public sealed record GitHubBudgetSnapshot(
    GitHubBudgetSessionKind SessionKind,
    string ScopeKey,
    GitHubRateWindow? Core,
    GitHubRateWindow? Search,
    GitHubRateWindow? GraphQl = null);

public sealed record ApiRequestObservation(
    string ScopeKey,
    string Resource,
    string Operation,
    bool IsNetwork,
    int StatusCode,
    long DurationMilliseconds,
    DateTimeOffset OccurredAt);
