using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.GitHub.Clients;

namespace RepoGalaxy.GitHub.Services;

/// <summary>
/// 仓库同步服务
/// 负责 GitHub API 数据与本地数据库的同步
/// </summary>
public class RepositorySyncService
{
    private readonly GitHubApiClient _gitHubClient;
    private readonly RepoGalaxyDbContext _dbContext;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger<RepositorySyncService>? _logger;
    
    public RepositorySyncService(
        GitHubApiClient gitHubClient,
        RepoGalaxyDbContext dbContext,
        RateLimiter rateLimiter,
        ILogger<RepositorySyncService>? logger = null)
    {
        _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _logger = logger;
    }
    
    /// <summary>
    /// 同步搜索结果到本地数据库
    /// </summary>
    public async Task<SyncResult> SyncSearchResultsAsync(string query, string? language = null, int maxResults = 50)
    {
        _logger?.LogInformation("Syncing search results for query: {Query}", query);
        
        await _rateLimiter.WaitAsync();
        var results = await _gitHubClient.SearchRepositoriesAsync(query, language);
        var repositories = results.Take(maxResults).ToList();
        
        return await SyncRepositoriesAsync(repositories);
    }
    
    /// <summary>
    /// 同步 Trending 仓库
    /// </summary>
    public async Task<SyncResult> SyncTrendingAsync(string? language = null, string since = "daily")
    {
        _logger?.LogInformation("Syncing trending repositories ({Since})", since);
        
        await _rateLimiter.WaitAsync();
        var results = await _gitHubClient.GetTrendingAsync(language, since);
        var repositories = results.ToList();
        
        return await SyncRepositoriesAsync(repositories);
    }
    
    /// <summary>
    /// 同步单个仓库详情
    /// </summary>
    public async Task<bool> SyncRepositoryAsync(string owner, string name)
    {
        _logger?.LogInformation("Syncing repository: {Owner}/{Name}", owner, name);
        
        await _rateLimiter.WaitAsync();
        var repo = await _gitHubClient.GetRepositoryAsync(owner, name);
        
        if (repo == null)
        {
            _logger?.LogWarning("Repository not found: {Owner}/{Name}", owner, name);
            return false;
        }
        
        await SaveRepositoryAsync(repo);
        return true;
    }
    
    /// <summary>
    /// 同步用户自己的仓库
    /// </summary>
    public async Task<SyncResult> SyncUserRepositoriesAsync()
    {
        _logger?.LogInformation("Syncing user repositories");
        
        await _rateLimiter.WaitAsync();
        var results = await _gitHubClient.GetUserRepositoriesAsync();
        var repositories = results.ToList();
        
        return await SyncRepositoriesAsync(repositories);
    }
    
    /// <summary>
    /// 清理过期缓存
    /// </summary>
    public async Task<int> ClearOldCacheAsync(TimeSpan maxAge)
    {
        _logger?.LogInformation("Clearing cache older than {Days} days", maxAge.TotalDays);
        var cutoff = DateTimeOffset.Now - maxAge;
        var oldRepos = await _dbContext.Repositories
            .Where(r => r.CachedAt < cutoff && !r.IsBookmarked)
            .ToListAsync();
        
        _dbContext.Repositories.RemoveRange(oldRepos);
        return await _dbContext.SaveChangesAsync();
    }
    
    /// <summary>
    /// 批量同步仓库列表
    /// </summary>
    private async Task<SyncResult> SyncRepositoriesAsync(IEnumerable<Repository> repositories)
    {
        var result = new SyncResult();
        var repos = repositories.ToList();
        
        foreach (var repo in repos)
        {
            try
            {
                var isNew = await SaveRepositoryAsync(repo);
                if (isNew)
                    result.Added++;
                else
                    result.Updated++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to sync repository: {FullName}", repo.FullName);
                result.Failed++;
            }
        }
        
        result.Total = repos.Count;
        _logger?.LogInformation("Sync completed: {Added} added, {Updated} updated, {Failed} failed", 
            result.Added, result.Updated, result.Failed);
        
        return result;
    }
    
    /// <summary>
    /// 保存仓库到数据库（新增或更新）
    /// </summary>
    /// <returns>是否为新仓库</returns>
    private async Task<bool> SaveRepositoryAsync(Repository repo)
    {
        var existing = await _dbContext.Repositories
            .FirstOrDefaultAsync(r => r.Owner == repo.Owner && r.Name == repo.Name);
        
        var entity = MapToEntity(repo);
        
        if (existing != null)
        {
            // 更新现有记录
            existing.GitHubId = entity.GitHubId;
            existing.Description = entity.Description;
            existing.PrimaryLanguage = entity.PrimaryLanguage;
            existing.Stars = entity.Stars;
            existing.Forks = entity.Forks;
            existing.Watchers = entity.Watchers;
            existing.OpenIssues = entity.OpenIssues;
            existing.UpdatedAt = entity.UpdatedAt;
            existing.LastPushedAt = entity.LastPushedAt;
            existing.CachedAt = DateTimeOffset.Now;
            existing.DiscoveryScore = entity.DiscoveryScore;
            existing.TopicsJson = entity.TopicsJson;
            existing.LanguagesJson = entity.LanguagesJson;
            
            _dbContext.Repositories.Update(existing);
            await _dbContext.SaveChangesAsync();
            return false;
        }
        else
        {
            // 新增记录
            entity.CachedAt = DateTimeOffset.Now;
            _dbContext.Repositories.Add(entity);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
    
    private static RepositoryEntity MapToEntity(Repository repo)
    {
        return new RepositoryEntity
        {
            GitHubId = repo.GitHubId,
            Owner = repo.Owner,
            Name = repo.Name,
            Description = repo.Description,
            PrimaryLanguage = repo.PrimaryLanguage,
            TopicsJson = System.Text.Json.JsonSerializer.Serialize(repo.Topics),
            HtmlUrl = repo.HtmlUrl,
            Stars = repo.Stars,
            Forks = repo.Forks,
            Watchers = repo.Watchers,
            OpenIssues = repo.OpenIssues,
            CreatedAt = repo.CreatedAt,
            UpdatedAt = repo.UpdatedAt,
            LastPushedAt = repo.LastPushedAt,
            DiscoveryScore = repo.DiscoveryScore,
            LanguagesJson = System.Text.Json.JsonSerializer.Serialize(repo.Languages)
        };
    }
}

/// <summary>
/// 同步结果
/// </summary>
public class SyncResult
{
    public int Total { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
    
    public bool IsSuccess => Failed == 0;
}
