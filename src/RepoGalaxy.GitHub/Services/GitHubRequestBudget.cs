using RepoGalaxy.Core.Models;

namespace RepoGalaxy.GitHub.Services;

public sealed class GitHubRequestBudget
{
    private readonly object _gate = new();
    private GitHubRateLimit? _latest;
    public GitHubRateLimit? Latest { get { lock (_gate) return _latest; } }
    public void Update(GitHubRateLimit? limit) { if (limit != null) lock (_gate) _latest = limit; }
    public void Update(GitHubRateWindow? window)
    {
        if (window is null) return;
        lock (_gate)
        {
            var current = _latest ?? new GitHubRateLimit();
            _latest = window.Resource.Equals("search", StringComparison.OrdinalIgnoreCase)
                ? new GitHubRateLimit { CoreLimit = current.CoreLimit, CoreRemaining = current.CoreRemaining, CoreResetAt = current.CoreResetAt, SearchLimit = window.Limit, SearchRemaining = window.Remaining, SearchResetAt = window.ResetAt }
                : new GitHubRateLimit { CoreLimit = window.Limit, CoreRemaining = window.Remaining, CoreResetAt = window.ResetAt, SearchLimit = current.SearchLimit, SearchRemaining = current.SearchRemaining, SearchResetAt = current.SearchResetAt };
        }
    }
    public bool CanSearch(out DateTimeOffset resetAt) { var limit = Latest; resetAt = limit?.SearchResetAt ?? DateTimeOffset.MinValue; return limit == null || limit.SearchRemaining > 0; }
    public bool CanCore(out DateTimeOffset resetAt) { var limit = Latest; resetAt = limit?.CoreResetAt ?? DateTimeOffset.MinValue; return limit == null || limit.CoreRemaining > 0; }
}
