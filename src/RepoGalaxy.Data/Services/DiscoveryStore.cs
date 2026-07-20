using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

public sealed class DiscoveryStore
{
    private readonly RepoGalaxyDbContext _db;
    public DiscoveryStore(RepoGalaxyDbContext db) => _db = db;

    public async Task<IReadOnlyList<DiscoverySubscription>> GetSubscriptionsAsync() =>
        (await _db.DiscoverySubscriptions.OrderBy(s => s.Name).ToListAsync()).Select(Map).ToList();

    public async Task SaveSubscriptionAsync(DiscoverySubscription subscription)
    {
        var entity = subscription.Id == 0 ? new DiscoverySubscriptionEntity() : await _db.DiscoverySubscriptions.FindAsync(subscription.Id) ?? new DiscoverySubscriptionEntity();
        entity.Name = subscription.Name.Trim();
        entity.TopicsJson = JsonSerializer.Serialize(subscription.Topics.Distinct(StringComparer.OrdinalIgnoreCase));
        entity.LanguagesJson = JsonSerializer.Serialize(subscription.Languages.Distinct(StringComparer.OrdinalIgnoreCase));
        entity.KeywordsJson = JsonSerializer.Serialize(subscription.Keywords.Distinct(StringComparer.OrdinalIgnoreCase));
        entity.IsEnabled = subscription.IsEnabled;
        entity.NotificationThreshold = subscription.NotificationThreshold;
        entity.LastSyncedAt = subscription.LastSyncedAt;
        if (entity.Id == 0) _db.DiscoverySubscriptions.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteSubscriptionAsync(long id)
    {
        var item = await _db.DiscoverySubscriptions.FindAsync(id);
        if (item != null) { _db.DiscoverySubscriptions.Remove(item); await _db.SaveChangesAsync(); }
    }

    public async Task<IReadOnlyList<FeedItem>> GetFeedAsync(FeedSource source, bool unreadOnly = false)
    {
        var query = _db.FeedItems.Include(f => f.Repository).Where(f => f.Source == (int)source && !f.IsDismissed);
        if (unreadOnly) query = query.Where(f => !f.IsRead);
        return (await query.OrderByDescending(f => f.DiscoveredAt).Take(100).ToListAsync()).Select(Map).ToList();
    }

    public async Task<IReadOnlyList<FeedItem>> GetNotificationsAsync() =>
        (await _db.FeedItems.Include(f => f.Repository).Where(f => !f.IsDismissed && (!f.IsRead || f.NotificationDelivered)).OrderByDescending(f => f.DiscoveredAt).Take(100).ToListAsync()).Select(Map).ToList();

    public async Task<bool> AddFeedItemAsync(Repository repository, FeedSource source, FeedReason reason)
    {
        var repo = await UpsertRepositoryAsync(repository);
        var existing = await _db.FeedItems.FirstOrDefaultAsync(f => f.RepositoryId == repo.Id && f.Source == (int)source);
        if (existing != null) return false;
        _db.FeedItems.Add(new FeedItemEntity { RepositoryId = repo.Id, Source = (int)source, Reason = reason.Summary, MatchedRule = reason.MatchedRule, Score = reason.Score, DiscoveredAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task MarkReadAsync(long id, bool dismissed = false)
    {
        var item = await _db.FeedItems.FindAsync(id);
        if (item != null) { item.IsRead = true; item.IsDismissed = dismissed; await _db.SaveChangesAsync(); }
    }

    public async Task<IReadOnlyList<Repository>> GetSavedRepositoriesAsync()
    {
        var rows = await _db.Bookmarks.Include(b => b.Repository).Include(b => b.Tags).OrderByDescending(b => b.BookmarkedAt).ToListAsync();
        return rows.Select(b => MapRepository(b.Repository)).ToList();
    }

    public async Task ToggleSavedAsync(long repositoryId, string collection = "Library", string? note = null, IEnumerable<string>? tags = null)
    {
        var saved = await _db.Bookmarks.Include(b => b.Tags).FirstOrDefaultAsync(b => b.RepositoryId == repositoryId);
        if (saved != null) { _db.Bookmarks.Remove(saved); await _db.SaveChangesAsync(); return; }
        saved = new BookmarkEntity { RepositoryId = repositoryId, BookmarkedAt = DateTimeOffset.UtcNow, CollectionName = collection, Notes = note };
        foreach (var tag in tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase) ?? []) saved.Tags.Add(new BookmarkTagEntity { Name = tag.Trim() });
        _db.Bookmarks.Add(saved);
        var repository = await _db.Repositories.FindAsync(repositoryId);
        if (repository != null) repository.IsBookmarked = true;
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Repository>> GetRepositoryCandidatesAsync() => (await _db.Repositories.Where(r => !r.IsIgnored).OrderByDescending(r => r.DiscoveryScore).Take(100).ToListAsync()).Select(MapRepository).ToList();

    public async Task<IReadOnlyList<Repository>> GetSavedForReleaseChecksAsync() => await GetSavedRepositoriesAsync();
    public async Task<bool> TryRecordReleaseAsync(long repositoryId, ReleaseInfo release)
    {
        if (await _db.ReleaseNotifications.AnyAsync(r => r.RepositoryId == repositoryId && r.ReleaseId == release.Id)) return false;
        _db.ReleaseNotifications.Add(new ReleaseNotificationEntity { RepositoryId = repositoryId, ReleaseId = release.Id, PublishedAt = release.PublishedAt });
        await _db.SaveChangesAsync(); return true;
    }

    private async Task<RepositoryEntity> UpsertRepositoryAsync(Repository model)
    {
        var entity = await _db.Repositories.FirstOrDefaultAsync(r => r.Owner == model.Owner && r.Name == model.Name);
        if (entity == null) { entity = new RepositoryEntity { Owner = model.Owner, Name = model.Name }; _db.Repositories.Add(entity); }
        entity.GitHubId = model.GitHubId; entity.Description = model.Description; entity.PrimaryLanguage = model.PrimaryLanguage; entity.HtmlUrl = model.HtmlUrl; entity.Stars = model.Stars; entity.Forks = model.Forks; entity.Watchers = model.Watchers; entity.OpenIssues = model.OpenIssues; entity.CreatedAt = model.CreatedAt; entity.UpdatedAt = model.UpdatedAt; entity.LastPushedAt = model.LastPushedAt; entity.DiscoveryScore = model.DiscoveryScore; entity.TopicsJson = JsonSerializer.Serialize(model.Topics); entity.LanguagesJson = JsonSerializer.Serialize(model.Languages); entity.CachedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(); return entity;
    }

    private static DiscoverySubscription Map(DiscoverySubscriptionEntity x) => new() { Id = x.Id, Name = x.Name, Topics = Read(x.TopicsJson), Languages = Read(x.LanguagesJson), Keywords = Read(x.KeywordsJson), IsEnabled = x.IsEnabled, NotificationThreshold = x.NotificationThreshold, LastSyncedAt = x.LastSyncedAt };
    private static FeedItem Map(FeedItemEntity x) => new() { Id = x.Id, RepositoryId = x.RepositoryId, Repository = MapRepository(x.Repository), Source = (FeedSource)x.Source, Reason = new FeedReason { Summary = x.Reason, MatchedRule = x.MatchedRule, Score = x.Score }, DiscoveredAt = x.DiscoveredAt, IsRead = x.IsRead, IsDismissed = x.IsDismissed };
    private static Repository MapRepository(RepositoryEntity x) => new() { Id = x.Id, GitHubId = x.GitHubId, Owner = x.Owner, Name = x.Name, Description = x.Description ?? string.Empty, PrimaryLanguage = x.PrimaryLanguage ?? string.Empty, HtmlUrl = x.HtmlUrl ?? string.Empty, Stars = x.Stars, Forks = x.Forks, Watchers = x.Watchers, OpenIssues = x.OpenIssues, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt, LastPushedAt = x.LastPushedAt, DiscoveryScore = x.DiscoveryScore, Topics = Read(x.TopicsJson), CachedAt = x.CachedAt };
    private static List<string> Read(string? value) { try { return JsonSerializer.Deserialize<List<string>>(value ?? "[]") ?? []; } catch { return []; } }
}
