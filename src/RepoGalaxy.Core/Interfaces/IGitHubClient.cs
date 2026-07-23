using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

public interface IApiRequestTelemetry
{
    void Record(ApiRequestObservation observation);
}

public interface IGitHubContributionService
{
    Task<ContributionCalendarSnapshot?> GetCalendarAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// GitHub API 客户端接口
/// </summary>
public interface IGitHubClient
{
    // 认证
    Task<bool> IsAuthenticatedAsync();
    Task<User?> GetCurrentUserAsync();
    Task<User?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<GitHubRateLimit?> GetRateLimitAsync();
    Task<IReadOnlyList<UserSocialAccount>> GetUserSocialAccountsAsync(string login, CancellationToken cancellationToken = default);
    
    // 仓库查询
    Task<Repository?> GetRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Repository>> SearchRepositoriesAsync(string query, string? language = null, string? sort = null);
    Task<GitHubPage<Repository>> SearchRepositoriesPageAsync(string query, string? language = null, string? sort = null, string? nextPageUrl = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Repository>> GetTrendingAsync(string? language = null, string since = "daily", CancellationToken cancellationToken = default);
    Task<IEnumerable<Repository>> GetUserRepositoriesAsync();
    Task<GitHubPage<Repository>> GetUserRepositoriesPageAsync(string? nextPageUrl = null, CancellationToken cancellationToken = default);
    Task<GitHubPage<Repository>> GetStarredRepositoriesPageAsync(string? nextPageUrl = null, CancellationToken cancellationToken = default);
    Task<ReleaseInfo?> GetLatestReleaseAsync(string owner, string name, CancellationToken cancellationToken = default);
    
    // 语言统计
    Task<List<LanguageInfo>> GetLanguagesAsync(string owner, string name, CancellationToken cancellationToken = default);
    Task<string?> GetReadmeAsync(string owner, string name, CancellationToken cancellationToken = default);
    
    // 社交操作
    Task<bool> StarRepositoryAsync(string owner, string name);
    Task<bool> UnstarRepositoryAsync(string owner, string name);
    Task<bool> IsStarredAsync(string owner, string name);
}
