using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Data.Services;

namespace RepoGalaxy.Data.Repositories;

/// <summary>Compatibility repository backed exclusively by short-lived contexts.</summary>
public sealed class RepositoryRepository
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    public RepositoryRepository(IDbContextFactory<RepoGalaxyDbContext> factory) => _factory = factory;

    public async Task<RepositoryEntity?> GetByIdAsync(long id) { await using var db = await _factory.CreateDbContextAsync(); return await db.Repositories.AsNoTracking().Include(x => x.Bookmarks).FirstOrDefaultAsync(x => x.Id == id); }
    public async Task<RepositoryEntity?> GetByFullNameAsync(string owner, string name) { await using var db = await _factory.CreateDbContextAsync(); return await db.Repositories.AsNoTracking().Include(x => x.Bookmarks).FirstOrDefaultAsync(x => x.Owner == owner && x.Name == name); }
    public async Task<IEnumerable<RepositoryEntity>> GetAllAsync(int page = 1, int pageSize = 50) { await using var db = await _factory.CreateDbContextAsync(); return await db.Repositories.AsNoTracking().OrderByDescending(x => x.CachedAt).Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToListAsync(); }
    public async Task<IEnumerable<RepositoryEntity>> GetBookmarksAsync() { await using var db = await _factory.CreateDbContextAsync(); return await db.Repositories.AsNoTracking().Where(x => x.IsBookmarked).OrderByDescending(x => x.LastViewedAt).ToListAsync(); }
    public async Task<IEnumerable<RepositoryEntity>> GetCachedAsync(TimeSpan? maxAge = null) { await using var db = await _factory.CreateDbContextAsync(); var cutoff = DateTimeOffset.UtcNow - (maxAge ?? TimeSpan.FromHours(24)); return await db.Repositories.AsNoTracking().Where(x => x.CachedAt > cutoff && !x.IsIgnored).OrderByDescending(x => x.CachedAt).ToListAsync(); }
    public async Task<IEnumerable<RepositoryEntity>> SearchAsync(string keyword) { await using var db = await _factory.CreateDbContextAsync(); var value = keyword.Trim().ToLowerInvariant(); return await db.Repositories.AsNoTracking().Where(x => x.Name.ToLower().Contains(value) || x.Owner.ToLower().Contains(value) || x.Description != null && x.Description.ToLower().Contains(value)).OrderByDescending(x => x.Stars).ToListAsync(); }

    public async Task<RepositoryEntity> AddOrUpdateAsync(RepositoryEntity entity)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Repositories.FirstOrDefaultAsync(x => x.Owner == entity.Owner && x.Name == entity.Name);
        if (existing is null) { entity.CachedAt = DateTimeOffset.UtcNow; db.Repositories.Add(entity); await db.SaveChangesWithRetryAsync(); return entity; }
        existing.GitHubId = entity.GitHubId; existing.Description = entity.Description; existing.PrimaryLanguage = entity.PrimaryLanguage; existing.Stars = entity.Stars; existing.Forks = entity.Forks; existing.Watchers = entity.Watchers; existing.OpenIssues = entity.OpenIssues; existing.UpdatedAt = entity.UpdatedAt; existing.LastPushedAt = entity.LastPushedAt; existing.CachedAt = DateTimeOffset.UtcNow; existing.DiscoveryScore = entity.DiscoveryScore; existing.LanguagesJson = entity.LanguagesJson; existing.TopicsJson = entity.TopicsJson;
        await db.SaveChangesWithRetryAsync(); return existing;
    }
    public async Task<bool> BookmarkAsync(long repositoryId, string collection = "默认收藏") { await using var db = await _factory.CreateDbContextAsync(); var repo = await db.Repositories.FindAsync(repositoryId); if (repo is null) return false; repo.IsBookmarked = true; var bookmark = await db.Bookmarks.FirstOrDefaultAsync(x => x.RepositoryId == repositoryId); if (bookmark is null) db.Bookmarks.Add(new BookmarkEntity { RepositoryId = repositoryId, BookmarkedAt = DateTimeOffset.UtcNow, CollectionName = collection }); else bookmark.CollectionName = collection; await db.SaveChangesWithRetryAsync(); return true; }
    public async Task<bool> UnbookmarkAsync(long repositoryId) { await using var db = await _factory.CreateDbContextAsync(); var repo = await db.Repositories.FindAsync(repositoryId); if (repo is null) return false; repo.IsBookmarked = false; var bookmark = await db.Bookmarks.FirstOrDefaultAsync(x => x.RepositoryId == repositoryId); if (bookmark is not null) db.Remove(bookmark); await db.SaveChangesWithRetryAsync(); return true; }
    public async Task RecordViewAsync(long repositoryId, int source, TimeSpan? duration = null) { await using var db = await _factory.CreateDbContextAsync(); var repo = await db.Repositories.FindAsync(repositoryId); if (repo is null) return; repo.ViewCount++; repo.LastViewedAt = DateTimeOffset.UtcNow; db.ViewHistories.Add(new ViewHistoryEntity { RepositoryId = repositoryId, ViewedAt = DateTimeOffset.UtcNow, DurationSeconds = (long)(duration?.TotalSeconds ?? 0), Source = source }); await db.SaveChangesWithRetryAsync(); }
    public async Task<int> ClearOldCacheAsync(TimeSpan maxAge) { await using var db = await _factory.CreateDbContextAsync(); var cutoff = DateTimeOffset.UtcNow - maxAge; return await db.Repositories.Where(x => x.CachedAt < cutoff && !x.IsBookmarked).ExecuteDeleteAsync(); }
}
