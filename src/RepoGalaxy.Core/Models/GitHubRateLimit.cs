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
