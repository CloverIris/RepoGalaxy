using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Repositories;

/// <summary>
/// 仓库数据仓储
/// </summary>
public class RepositoryRepository
{
    private readonly RepoGalaxyDbContext _context;
    
    public RepositoryRepository(RepoGalaxyDbContext context)
    {
        _context = context;
    }
    
    public async Task<RepositoryEntity?> GetByIdAsync(long id)
    {
        return await _context.Repositories
            .Include(r => r.Bookmarks)
            .FirstOrDefaultAsync(r => r.Id == id);
    }
    
    public async Task<RepositoryEntity?> GetByFullNameAsync(string owner, string name)
    {
        return await _context.Repositories
            .Include(r => r.Bookmarks)
            .FirstOrDefaultAsync(r => r.Owner == owner && r.Name == name);
    }
    
    public async Task<IEnumerable<RepositoryEntity>> GetAllAsync(int page = 1, int pageSize = 50)
    {
        var allRepos = await _context.Repositories.ToListAsync();
        return allRepos
            .OrderByDescending(r => r.CachedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }
    
    public async Task<IEnumerable<RepositoryEntity>> GetBookmarksAsync()
    {
        var repos = await _context.Repositories
            .Where(r => r.IsBookmarked)
            .ToListAsync();
        return repos.OrderByDescending(r => r.LastViewedAt);
    }
    
    public async Task<IEnumerable<RepositoryEntity>> GetCachedAsync(TimeSpan? maxAge = null)
    {
        var cutoff = DateTimeOffset.Now - (maxAge ?? TimeSpan.FromHours(24));
        var allRepos = await _context.Repositories.ToListAsync();
        return allRepos
            .Where(r => r.CachedAt > cutoff && !r.IsIgnored)
            .OrderByDescending(r => r.CachedAt);
    }
    
    public async Task<IEnumerable<RepositoryEntity>> SearchAsync(string keyword)
    {
        var lowerKeyword = keyword.ToLower();
        return await _context.Repositories
            .Where(r => r.Name.ToLower().Contains(lowerKeyword) ||
                       r.Description.ToLower().Contains(lowerKeyword) ||
                       r.Owner.ToLower().Contains(lowerKeyword))
            .OrderByDescending(r => r.Stars)
            .ToListAsync();
    }
    
    public async Task<RepositoryEntity> AddOrUpdateAsync(RepositoryEntity entity)
    {
        var existing = await GetByFullNameAsync(entity.Owner, entity.Name);
        
        if (existing != null)
        {
            // 更新现有记录
            existing.GitHubId = entity.GitHubId;
            existing.Description = entity.Description;
            existing.Stars = entity.Stars;
            existing.Forks = entity.Forks;
            existing.Watchers = entity.Watchers;
            existing.OpenIssues = entity.OpenIssues;
            existing.UpdatedAt = entity.UpdatedAt;
            existing.LastPushedAt = entity.LastPushedAt;
            existing.CachedAt = DateTimeOffset.Now;
            existing.DiscoveryScore = entity.DiscoveryScore;
            existing.LanguagesJson = entity.LanguagesJson;
            existing.TopicsJson = entity.TopicsJson;
            
            _context.Repositories.Update(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
        else
        {
            entity.CachedAt = DateTimeOffset.Now;
            _context.Repositories.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }
    }
    
    public async Task<bool> BookmarkAsync(long repositoryId, string collection = "默认收藏")
    {
        var repo = await GetByIdAsync(repositoryId);
        if (repo == null) return false;
        
        repo.IsBookmarked = true;
        
        var bookmark = await _context.Bookmarks
            .FirstOrDefaultAsync(b => b.RepositoryId == repositoryId);
        
        if (bookmark == null)
        {
            bookmark = new BookmarkEntity
            {
                RepositoryId = repositoryId,
                BookmarkedAt = DateTimeOffset.Now,
                CollectionName = collection
            };
            _context.Bookmarks.Add(bookmark);
        }
        else
        {
            bookmark.CollectionName = collection;
        }
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<bool> UnbookmarkAsync(long repositoryId)
    {
        var repo = await GetByIdAsync(repositoryId);
        if (repo == null) return false;
        
        repo.IsBookmarked = false;
        
        var bookmark = await _context.Bookmarks
            .FirstOrDefaultAsync(b => b.RepositoryId == repositoryId);
        
        if (bookmark != null)
        {
            _context.Bookmarks.Remove(bookmark);
        }
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task RecordViewAsync(long repositoryId, int source, TimeSpan? duration = null)
    {
        var repo = await GetByIdAsync(repositoryId);
        if (repo == null) return;
        
        repo.ViewCount++;
        repo.LastViewedAt = DateTimeOffset.Now;
        
        var history = new ViewHistoryEntity
        {
            RepositoryId = repositoryId,
            ViewedAt = DateTimeOffset.Now,
            DurationSeconds = (long)(duration?.TotalSeconds ?? 0),
            Source = source
        };
        
        _context.ViewHistories.Add(history);
        await _context.SaveChangesAsync();
    }
    
    public async Task<int> ClearOldCacheAsync(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.Now - maxAge;
        var allRepos = await _context.Repositories.ToListAsync();
        var oldRecords = allRepos
            .Where(r => r.CachedAt < cutoff && !r.IsBookmarked)
            .ToList();
        
        _context.Repositories.RemoveRange(oldRecords);
        return await _context.SaveChangesAsync();
    }
}
