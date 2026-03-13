using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Data.Repositories;
using System.Text.Json;

namespace RepoGalaxy.Data.Services;

public class RepositoryService : IRepositoryService
{
    private readonly RepositoryRepository _repository;
    private readonly RepoGalaxyDbContext _context;
    
    public RepositoryService(RepoGalaxyDbContext context)
    {
        _context = context;
        _repository = new RepositoryRepository(context);
    }
    
    public async Task<Repository?> GetByIdAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity != null ? MapToModel(entity) : null;
    }
    
    public async Task<Repository?> GetByFullNameAsync(string owner, string name)
    {
        var entity = await _repository.GetByFullNameAsync(owner, name);
        return entity != null ? MapToModel(entity) : null;
    }
    
    public async Task<IEnumerable<Repository>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Select(MapToModel);
    }
    
    public async Task<IEnumerable<Repository>> GetBookmarksAsync()
    {
        var entities = await _repository.GetBookmarksAsync();
        return entities.Select(MapToModel);
    }
    
    public async Task<IEnumerable<Repository>> SearchAsync(string keyword)
    {
        var entities = await _repository.SearchAsync(keyword);
        return entities.Select(MapToModel);
    }
    
    public async Task<IEnumerable<Repository>> GetDiscoveryFeedAsync(int page = 1, int pageSize = 50)
    {
        var entities = await _context.Repositories
            .Where(r => !r.IsIgnored)
            .OrderByDescending(r => r.DiscoveryScore)
            .ThenByDescending(r => r.Stars)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return entities.Select(MapToModel);
    }
    
    public async Task<IEnumerable<Repository>> GetTrendingAsync(string? language = null, string since = "daily")
    {
        var cutoff = since.ToLower() switch
        {
            "daily" => DateTimeOffset.Now.AddDays(-1),
            "weekly" => DateTimeOffset.Now.AddDays(-7),
            "monthly" => DateTimeOffset.Now.AddDays(-30),
            _ => DateTimeOffset.Now.AddDays(-1)
        };
        
        var query = _context.Repositories.Where(r => r.CachedAt > cutoff && !r.IsIgnored);
        if (!string.IsNullOrEmpty(language))
            query = query.Where(r => r.PrimaryLanguage == language);
        
        var entities = await query.OrderByDescending(r => r.Stars).Take(30).ToListAsync();
        return entities.Select(MapToModel);
    }
    
    public async Task<bool> BookmarkAsync(long repositoryId, string collection = "默认收藏")
    {
        return await _repository.BookmarkAsync(repositoryId, collection);
    }
    
    public async Task<bool> UnbookmarkAsync(long repositoryId)
    {
        return await _repository.UnbookmarkAsync(repositoryId);
    }
    
    public async Task<bool> IgnoreAsync(long repositoryId)
    {
        var repo = await _repository.GetByIdAsync(repositoryId);
        if (repo == null) return false;
        repo.IsIgnored = true;
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task RecordViewAsync(long repositoryId, ViewSource source, TimeSpan? duration = null)
    {
        await _repository.RecordViewAsync(repositoryId, (int)source, duration);
    }
    
    public async Task<IEnumerable<Repository>> GetCachedAsync(TimeSpan? maxAge = null)
    {
        var entities = await _repository.GetCachedAsync(maxAge);
        return entities.Select(MapToModel);
    }
    
    public async Task RefreshCacheAsync()
    {
        await _repository.ClearOldCacheAsync(TimeSpan.FromDays(7));
    }
    
    public async Task ClearCacheAsync()
    {
        var all = await _context.Repositories.Where(r => !r.IsBookmarked).ToListAsync();
        _context.Repositories.RemoveRange(all);
        await _context.SaveChangesAsync();
    }
    
    public async Task SaveRepositoriesAsync(IEnumerable<Repository> repositories)
    {
        foreach (var repo in repositories)
        {
            var entity = MapToEntity(repo);
            await _repository.AddOrUpdateAsync(entity);
        }
    }
    
    private static Repository MapToModel(RepositoryEntity entity)
    {
        var repo = new Repository
        {
            Id = entity.Id,
            GitHubId = entity.GitHubId,
            Owner = entity.Owner,
            Name = entity.Name,
            HtmlUrl = entity.HtmlUrl ?? string.Empty,
            Description = entity.Description ?? string.Empty,
            PrimaryLanguage = entity.PrimaryLanguage ?? "Unknown",
            Topics = ParseJsonList(entity.TopicsJson),
            Stars = entity.Stars,
            Forks = entity.Forks,
            Watchers = entity.Watchers,
            OpenIssues = entity.OpenIssues,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            LastPushedAt = entity.LastPushedAt,
            DiscoveryScore = entity.DiscoveryScore,
            Orbit = (OrbitCategory)entity.OrbitCategoryId,
            IsBookmarked = entity.IsBookmarked,
            IsIgnored = entity.IsIgnored,
            LastViewedAt = entity.LastViewedAt,
            ViewCount = entity.ViewCount,
            CachedAt = entity.CachedAt,
            Languages = ParseLanguages(entity.LanguagesJson)
        };
        repo.CalculateSize();
        return repo;
    }
    
    private static RepositoryEntity MapToEntity(Repository model)
    {
        return new RepositoryEntity
        {
            Id = model.Id,
            GitHubId = model.GitHubId,
            Owner = model.Owner,
            Name = model.Name,
            HtmlUrl = model.HtmlUrl,
            Description = model.Description,
            PrimaryLanguage = model.PrimaryLanguage,
            TopicsJson = JsonSerializer.Serialize(model.Topics),
            Stars = model.Stars,
            Forks = model.Forks,
            Watchers = model.Watchers,
            OpenIssues = model.OpenIssues,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            LastPushedAt = model.LastPushedAt,
            OrbitCategoryId = (int)model.Orbit,
            DiscoveryScore = model.DiscoveryScore,
            IsBookmarked = model.IsBookmarked,
            IsIgnored = model.IsIgnored,
            LastViewedAt = model.LastViewedAt,
            ViewCount = model.ViewCount,
            CachedAt = model.CachedAt,
            LanguagesJson = JsonSerializer.Serialize(model.Languages)
        };
    }
    
    private static List<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }
    
    private static List<LanguageInfo> ParseLanguages(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<LanguageInfo>();
        try { return JsonSerializer.Deserialize<List<LanguageInfo>>(json) ?? new List<LanguageInfo>(); }
        catch { return new List<LanguageInfo>(); }
    }

    // Desktop ViewModel 使用的辅助方法
    
    public async Task<IEnumerable<Repository>> GetAllRepositoriesAsync()
    {
        return await GetDiscoveryFeedAsync(1, 100);
    }

    public async Task<IEnumerable<Repository>> SearchRepositoriesAsync(string keyword)
    {
        return await SearchAsync(keyword);
    }

    public async Task ToggleBookmarkAsync(long repositoryId)
    {
        var repo = await GetByIdAsync(repositoryId);
        if (repo == null) return;
        
        if (repo.IsBookmarked)
            await UnbookmarkAsync(repositoryId);
        else
            await BookmarkAsync(repositoryId);
    }

    // 本地仓库相关方法
    public async Task<IEnumerable<LocalRepository>> GetLocalRepositoriesAsync()
    {
        // SQLite 不支持 DateTimeOffset 的 ORDER BY，所以在客户端排序
        var entities = await _context.LocalRepositories
            .ToListAsync();
        return entities
            .OrderByDescending(r => r.AddedAt)
            .Select(MapToLocalModel);
    }

    public async Task<LocalRepository?> GetLocalRepositoryByPathAsync(string path)
    {
        var entity = await _context.LocalRepositories
            .FirstOrDefaultAsync(r => r.LocalPath == path);
        return entity != null ? MapToLocalModel(entity) : null;
    }

    public async Task AddLocalRepositoryAsync(string path, string name)
    {
        var entity = new LocalRepositoryEntity
        {
            Name = name,
            LocalPath = path,
            AddedAt = DateTimeOffset.UtcNow
        };
        _context.LocalRepositories.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveLocalRepositoryAsync(long id)
    {
        var entity = await _context.LocalRepositories.FindAsync(id);
        if (entity != null)
        {
            _context.LocalRepositories.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    private static LocalRepository MapToLocalModel(LocalRepositoryEntity entity)
    {
        return new LocalRepository
        {
            Id = entity.Id,
            Name = entity.Name,
            LocalPath = entity.LocalPath,
            GitHubUrl = entity.GitHubUrl,
            AddedAt = entity.AddedAt,
            IsTracked = entity.IsTracked
        };
    }
}
