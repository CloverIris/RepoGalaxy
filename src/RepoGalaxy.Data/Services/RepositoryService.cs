using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

public sealed class RepositoryService : IRepositoryService
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    public RepositoryService(IDbContextFactory<RepoGalaxyDbContext> factory) => _factory = factory;

    public async Task<Repository?> GetByIdAsync(long id) { await using var db = await _factory.CreateDbContextAsync(); var entity = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); return entity is null ? null : Map(entity); }
    public async Task<Repository?> GetByFullNameAsync(string owner, string name) { await using var db = await _factory.CreateDbContextAsync(); var entity = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(x => x.Owner == owner && x.Name == name); return entity is null ? null : Map(entity); }
    public async Task<IEnumerable<Repository>> GetAllAsync() { await using var db = await _factory.CreateDbContextAsync(); return (await db.Repositories.AsNoTracking().OrderByDescending(x => x.CachedAt).Take(100).ToListAsync()).Select(Map).ToList(); }
    public async Task<IEnumerable<Repository>> GetBookmarksAsync() { await using var db = await _factory.CreateDbContextAsync(); return (await db.Repositories.AsNoTracking().Where(x => x.IsBookmarked).OrderByDescending(x => x.LastViewedAt).ToListAsync()).Select(Map).ToList(); }
    public async Task<IEnumerable<Repository>> SearchAsync(string keyword) { await using var db = await _factory.CreateDbContextAsync(); keyword = keyword.Trim().ToLowerInvariant(); return (await db.Repositories.AsNoTracking().Where(x => x.Name.ToLower().Contains(keyword) || x.Owner.ToLower().Contains(keyword) || (x.Description != null && x.Description.ToLower().Contains(keyword))).OrderByDescending(x => x.Stars).Take(100).ToListAsync()).Select(Map).ToList(); }
    public async Task<IEnumerable<Repository>> GetDiscoveryFeedAsync(int page = 1, int pageSize = 50) { await using var db = await _factory.CreateDbContextAsync(); return (await db.Repositories.AsNoTracking().Where(x => !x.IsIgnored).OrderByDescending(x => x.DiscoveryScore).ThenByDescending(x => x.Stars).Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToListAsync()).Select(Map).ToList(); }
    public async Task<IEnumerable<Repository>> GetTrendingAsync(string? language = null, string since = "daily") { await using var db = await _factory.CreateDbContextAsync(); var days = since.ToLowerInvariant() switch { "weekly" => 7, "monthly" => 30, _ => 1 }; var cutoff = DateTimeOffset.UtcNow.AddDays(-days); var query = db.Repositories.AsNoTracking().Where(x => x.CachedAt > cutoff && !x.IsIgnored); if (!string.IsNullOrWhiteSpace(language)) query = query.Where(x => x.PrimaryLanguage == language); return (await query.OrderByDescending(x => x.Stars).Take(30).ToListAsync()).Select(Map).ToList(); }
    public async Task<bool> BookmarkAsync(long repositoryId, string collection = "默认收藏") { await using var db = await _factory.CreateDbContextAsync(); var repo = await db.Repositories.FindAsync(repositoryId); if (repo is null) return false; repo.IsBookmarked = true; var bookmark = await db.Bookmarks.FirstOrDefaultAsync(x => x.RepositoryId == repositoryId); if (bookmark is null) db.Bookmarks.Add(new BookmarkEntity { RepositoryId = repositoryId, CollectionName = collection, BookmarkedAt = DateTimeOffset.UtcNow }); else bookmark.CollectionName = collection; await db.SaveChangesWithRetryAsync(); return true; }
    public async Task<bool> UnbookmarkAsync(long repositoryId) { await using var db = await _factory.CreateDbContextAsync(); var repo = await db.Repositories.FindAsync(repositoryId); if (repo is null) return false; repo.IsBookmarked = false; var bookmark = await db.Bookmarks.FirstOrDefaultAsync(x => x.RepositoryId == repositoryId); if (bookmark is not null) db.Remove(bookmark); await db.SaveChangesWithRetryAsync(); return true; }
    public async Task<bool> IgnoreAsync(long repositoryId) { await using var db = await _factory.CreateDbContextAsync(); var repo = await db.Repositories.FindAsync(repositoryId); if (repo is null) return false; repo.IsIgnored = true; await db.SaveChangesWithRetryAsync(); return true; }
    public async Task RecordViewAsync(long repositoryId, ViewSource source, TimeSpan? duration = null) { await using var db = await _factory.CreateDbContextAsync(); var repo = await db.Repositories.FindAsync(repositoryId); if (repo is null) return; repo.ViewCount++; repo.LastViewedAt = DateTimeOffset.UtcNow; db.ViewHistories.Add(new ViewHistoryEntity { RepositoryId = repositoryId, ViewedAt = DateTimeOffset.UtcNow, DurationSeconds = (long)(duration?.TotalSeconds ?? 0), Source = (int)source }); await db.SaveChangesWithRetryAsync(); }
    public async Task<IEnumerable<Repository>> GetCachedAsync(TimeSpan? maxAge = null) { await using var db = await _factory.CreateDbContextAsync(); var cutoff = DateTimeOffset.UtcNow - (maxAge ?? TimeSpan.FromHours(24)); return (await db.Repositories.AsNoTracking().Where(x => x.CachedAt > cutoff && !x.IsIgnored).OrderByDescending(x => x.CachedAt).ToListAsync()).Select(Map).ToList(); }
    public Task RefreshCacheAsync() => ClearOldAsync(TimeSpan.FromDays(7));
    public async Task ClearCacheAsync() { await using var db = await _factory.CreateDbContextAsync(); await db.Repositories.Where(x => !x.IsBookmarked && !x.FeedItems.Any()).ExecuteDeleteAsync(); }
    private async Task ClearOldAsync(TimeSpan age) { await using var db = await _factory.CreateDbContextAsync(); var cutoff = DateTimeOffset.UtcNow - age; await db.Repositories.Where(x => x.CachedAt < cutoff && !x.IsBookmarked && !x.FeedItems.Any()).ExecuteDeleteAsync(); }

    public async Task SaveRepositoriesAsync(IEnumerable<Repository> repositories)
    {
        var batch = repositories.ToList();
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var model in batch)
        {
            var entity = await db.Repositories.FirstOrDefaultAsync(x => x.GitHubId == model.GitHubId || (x.Owner == model.Owner && x.Name == model.Name));
            if (entity is null) { entity = new RepositoryEntity(); db.Repositories.Add(entity); }
            Copy(model, entity);
        }
        await db.SaveChangesWithRetryAsync();
        foreach (var model in batch)
        {
            var entity = await db.Repositories.FirstAsync(x => x.GitHubId == model.GitHubId || (x.Owner == model.Owner && x.Name == model.Name));
            await db.RepositoryTopics.Where(x => x.RepositoryId == entity.Id).ExecuteDeleteAsync();
            await db.RepositoryLanguages.Where(x => x.RepositoryId == entity.Id).ExecuteDeleteAsync();
            foreach (var topic in model.Topics.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)) db.RepositoryTopics.Add(new RepositoryTopicEntity { RepositoryId = entity.Id, Topic = topic.Trim().ToLowerInvariant() });
            foreach (var language in model.Languages.Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.First())) db.RepositoryLanguages.Add(new RepositoryLanguageEntity { RepositoryId = entity.Id, Language = language.Name, Bytes = language.Bytes, Percentage = language.Percentage });
            if (model.Languages.Count == 0 && !string.IsNullOrWhiteSpace(model.PrimaryLanguage)) db.RepositoryLanguages.Add(new RepositoryLanguageEntity { RepositoryId = entity.Id, Language = model.PrimaryLanguage, Percentage = 1 });
        }
        await db.SaveChangesWithRetryAsync(); await transaction.CommitAsync();
    }
    public Task<IEnumerable<Repository>> GetAllRepositoriesAsync() => GetAllAsync();
    public Task<IEnumerable<Repository>> SearchRepositoriesAsync(string keyword) => SearchAsync(keyword);
    public async Task ToggleBookmarkAsync(long repositoryId) { var repo = await GetByIdAsync(repositoryId); if (repo is null) return; if (repo.IsBookmarked) await UnbookmarkAsync(repositoryId); else await BookmarkAsync(repositoryId); }

    public async Task<IEnumerable<LocalRepository>> GetLocalRepositoriesAsync() { await using var db = await _factory.CreateDbContextAsync(); return (await db.LocalRepositories.AsNoTracking().OrderByDescending(x => x.AddedAt).ToListAsync()).Select(MapLocal).ToList(); }
    public async Task<LocalRepository?> GetLocalRepositoryByPathAsync(string path) { await using var db = await _factory.CreateDbContextAsync(); var entity = await db.LocalRepositories.AsNoTracking().FirstOrDefaultAsync(x => x.LocalPath == path); return entity is null ? null : MapLocal(entity); }
    public async Task AddLocalRepositoryAsync(string path, string name) { await using var db = await _factory.CreateDbContextAsync(); if (!await db.LocalRepositories.AnyAsync(x => x.LocalPath == path)) { db.Add(new LocalRepositoryEntity { Name = name, LocalPath = path, AddedAt = DateTimeOffset.UtcNow, IsTracked = true }); await db.SaveChangesWithRetryAsync(); } }
    public async Task RemoveLocalRepositoryAsync(long id) { await using var db = await _factory.CreateDbContextAsync(); await db.LocalRepositories.Where(x => x.Id == id).ExecuteDeleteAsync(); }

    private static void Copy(Repository m, RepositoryEntity e) { e.GitHubId = m.GitHubId; e.Owner = m.Owner; e.Name = m.Name; e.HtmlUrl = m.HtmlUrl; e.OwnerAvatarUrl = m.OwnerAvatarUrl; e.Description = m.Description; e.PrimaryLanguage = m.PrimaryLanguage; e.IsPrivate = m.IsPrivate; e.IsArchived = m.IsArchived; e.TopicsJson = JsonSerializer.Serialize(m.Topics); e.LanguagesJson = JsonSerializer.Serialize(m.Languages); e.Stars = m.Stars; e.Forks = m.Forks; e.Watchers = m.Watchers; e.OpenIssues = m.OpenIssues; e.CreatedAt = m.CreatedAt; e.UpdatedAt = m.UpdatedAt; e.LastPushedAt = m.LastPushedAt; e.DiscoveryScore = m.DiscoveryScore; e.CachedAt = DateTimeOffset.UtcNow; }
    internal static Repository Map(RepositoryEntity e) => new() { Id = e.Id, GitHubId = e.GitHubId, Owner = e.Owner, Name = e.Name, HtmlUrl = e.HtmlUrl ?? string.Empty, OwnerAvatarUrl = e.OwnerAvatarUrl ?? string.Empty, Description = e.Description ?? string.Empty, PrimaryLanguage = e.PrimaryLanguage ?? "Unknown", IsPrivate = e.IsPrivate, IsArchived = e.IsArchived, Topics = Read<List<string>>(e.TopicsJson) ?? [], Languages = Read<List<LanguageInfo>>(e.LanguagesJson) ?? [], Stars = e.Stars, Forks = e.Forks, Watchers = e.Watchers, OpenIssues = e.OpenIssues, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt, LastPushedAt = e.LastPushedAt, DiscoveryScore = e.DiscoveryScore, IsBookmarked = e.IsBookmarked, IsIgnored = e.IsIgnored, LastViewedAt = e.LastViewedAt, ViewCount = e.ViewCount, CachedAt = e.CachedAt };
    private static T? Read<T>(string? json) { try { return JsonSerializer.Deserialize<T>(json ?? string.Empty); } catch { return default; } }
    private static LocalRepository MapLocal(LocalRepositoryEntity e) => new() { Id = e.Id, Name = e.Name, LocalPath = e.LocalPath, GitHubUrl = e.GitHubUrl, IsTracked = e.IsTracked, AddedAt = e.AddedAt };
}
