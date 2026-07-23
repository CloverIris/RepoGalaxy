using RepoGalaxy.Core.Models;

namespace RepoGalaxy.GitHub.Services;

public sealed class GitHubRequestBudget
{
    private readonly object _gate = new();
    private GitHubBudgetSnapshot _snapshot = new(GitHubBudgetSessionKind.Guest, "guest", null, null);

    public event EventHandler<GitHubBudgetSnapshot>? Changed;
    public GitHubBudgetSnapshot Snapshot { get { lock (_gate) return _snapshot; } }
    public GitHubRateLimit? Latest
    {
        get
        {
            var snapshot = Snapshot;
            if (snapshot.Core is null && snapshot.Search is null) return null;
            return new GitHubRateLimit
            {
                CoreLimit = snapshot.Core?.Limit ?? 0,
                CoreRemaining = snapshot.Core?.Remaining ?? 0,
                CoreResetAt = snapshot.Core?.ResetAt ?? default,
                SearchLimit = snapshot.Search?.Limit ?? 0,
                SearchRemaining = snapshot.Search?.Remaining ?? 0,
                SearchResetAt = snapshot.Search?.ResetAt ?? default
            };
        }
    }

    public void BeginSession(GitHubBudgetSessionKind kind, string scopeKey, bool preserveObservedWindows = false)
    {
        GitHubBudgetSnapshot snapshot;
        lock (_gate)
        {
            snapshot = _snapshot = new(kind, string.IsNullOrWhiteSpace(scopeKey) ? "guest" : scopeKey,
                preserveObservedWindows ? _snapshot.Core : null,
                preserveObservedWindows ? _snapshot.Search : null);
        }
        Changed?.Invoke(this, snapshot);
    }

    public void Update(GitHubRateLimit? limit)
    {
        if (limit is null) return;
        Update(new GitHubRateWindow("core", limit.CoreLimit, limit.CoreRemaining, limit.CoreResetAt, limit.CoreUsed, limit.ObservedAt));
        Update(new GitHubRateWindow("search", limit.SearchLimit, limit.SearchRemaining, limit.SearchResetAt, limit.SearchUsed, limit.ObservedAt));
    }

    public void Update(GitHubRateWindow? window)
    {
        if (window is null) return;
        GitHubBudgetSnapshot snapshot;
        lock (_gate)
        {
            snapshot = _snapshot = window.Resource.Equals("search", StringComparison.OrdinalIgnoreCase)
                ? _snapshot with { Search = window }
                : _snapshot with { Core = window };
        }
        Changed?.Invoke(this, snapshot);
    }

    public bool CanSearch(out DateTimeOffset resetAt) { var value = Snapshot.Search; resetAt = value?.RetryAfter ?? value?.ResetAt ?? DateTimeOffset.MinValue; return value is null || value.Remaining > 0 && (value.RetryAfter is null || value.RetryAfter <= DateTimeOffset.UtcNow); }
    public bool CanCore(out DateTimeOffset resetAt) { var value = Snapshot.Core; resetAt = value?.RetryAfter ?? value?.ResetAt ?? DateTimeOffset.MinValue; return value is null || value.Remaining > 0 && (value.RetryAfter is null || value.RetryAfter <= DateTimeOffset.UtcNow); }
}
