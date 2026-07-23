namespace RepoGalaxy.Core.Models;

public enum ContributionDataSource
{
    None,
    GitHubFresh,
    GitHubStale,
    LocalGit
}

public enum ContributionLoadState
{
    Idle,
    Loading,
    Ready,
    Stale,
    Unavailable
}

public sealed record ContributionCalendarSnapshot(
    IReadOnlyList<ContributionDay> Days,
    int TotalContributions,
    int CurrentStreak,
    int WeekContributions,
    ContributionDataSource Source,
    DateTimeOffset FetchedAt,
    string? ErrorCode = null)
{
    public static ContributionCalendarSnapshot Empty(ContributionDataSource source = ContributionDataSource.None) =>
        new([], 0, 0, 0, source, DateTimeOffset.UtcNow);
}
