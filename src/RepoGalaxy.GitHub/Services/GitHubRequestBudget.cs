using RepoGalaxy.Core.Models;

namespace RepoGalaxy.GitHub.Services;

public sealed class GitHubRequestBudget
{
    private readonly object _gate = new();
    private GitHubRateLimit? _latest;
    public GitHubRateLimit? Latest { get { lock (_gate) return _latest; } }
    public void Update(GitHubRateLimit? limit) { if (limit != null) lock (_gate) _latest = limit; }
    public bool CanSearch(out DateTimeOffset resetAt) { var limit = Latest; resetAt = limit?.SearchResetAt ?? DateTimeOffset.MinValue; return limit == null || limit.SearchRemaining > 0; }
}
