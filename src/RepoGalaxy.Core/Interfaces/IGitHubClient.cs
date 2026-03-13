using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

/// <summary>
/// GitHub API 客户端接口
/// </summary>
public interface IGitHubClient
{
    // 认证
    Task<bool> IsAuthenticatedAsync();
    Task<User?> GetCurrentUserAsync();
    
    // 仓库查询
    Task<Repository?> GetRepositoryAsync(string owner, string name);
    Task<IEnumerable<Repository>> SearchRepositoriesAsync(string query, string? language = null, string? sort = null);
    Task<IEnumerable<Repository>> GetTrendingAsync(string? language = null, string since = "daily");
    Task<IEnumerable<Repository>> GetUserRepositoriesAsync();
    
    // 语言统计
    Task<List<LanguageInfo>> GetLanguagesAsync(string owner, string name);
    
    // 社交操作
    Task<bool> StarRepositoryAsync(string owner, string name);
    Task<bool> UnstarRepositoryAsync(string owner, string name);
    Task<bool> IsStarredAsync(string owner, string name);
}
