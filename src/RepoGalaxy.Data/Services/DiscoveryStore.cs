using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

/// <summary>Discovery persistence. Every operation owns a short-lived DbContext.</summary>
public sealed class DiscoveryStore
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    public DiscoveryStore(IDbContextFactory<RepoGalaxyDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<DiscoverySubscription>> GetSubscriptionsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return (await db.DiscoverySubscriptions.AsNoTracking().OrderBy(s => s.Name).ToListAsync()).Select(Map).ToList();
    }

    public async Task SaveSubscriptionAsync(DiscoverySubscription subscription)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var entity = subscription.Id == 0 ? new DiscoverySubscriptionEntity() : await db.DiscoverySubscriptions.FindAsync(subscription.Id) ?? new DiscoverySubscriptionEntity();
        entity.Name = subscription.Name.Trim(); entity.TopicsJson = JsonSerializer.Serialize(subscription.Topics.Distinct(StringComparer.OrdinalIgnoreCase)); entity.LanguagesJson = JsonSerializer.Serialize(subscription.Languages.Distinct(StringComparer.OrdinalIgnoreCase)); entity.KeywordsJson = JsonSerializer.Serialize(subscription.Keywords.Distinct(StringComparer.OrdinalIgnoreCase)); entity.IsEnabled = subscription.IsEnabled; entity.NotificationThreshold = subscription.NotificationThreshold; entity.LastSyncedAt = subscription.LastSyncedAt;
        if (entity.Id == 0) db.DiscoverySubscriptions.Add(entity);
        await db.SaveChangesWithRetryAsync();
    }

    public async Task DeleteSubscriptionAsync(long id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var item = await db.DiscoverySubscriptions.FindAsync(id);
        if (item != null) { db.DiscoverySubscriptions.Remove(item); await db.SaveChangesWithRetryAsync(); }
    }

    public async Task<IReadOnlyList<FeedItem>> GetFeedAsync(FeedSource source, bool unreadOnly = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.FeedItems.AsNoTracking().Include(f => f.Repository).Where(f => f.Source == (int)source && !f.IsDismissed);
        if (unreadOnly) query = query.Where(f => !f.IsRead);
        var rows = await query.ToListAsync();
        return rows.OrderByDescending(f => f.DiscoveredAt).Take(100).Select(Map).ToList();
    }

    public async Task<IReadOnlyList<FeedItem>> GetNotificationsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.FeedItems.AsNoTracking().Include(f => f.Repository)
            .Where(f => !f.IsDismissed && (!f.IsRead || f.NotificationDelivered)).ToListAsync();
        return rows.OrderByDescending(f => f.DiscoveredAt).Take(100).Select(Map).ToList();
    }

    public async Task<bool> AddFeedItemAsync(Repository repository, FeedSource source, FeedReason reason)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var repo = await UpsertRepositoryAsync(db, repository);
        // Persist a newly discovered repository before using its generated key
        // as the feed foreign key. Assigning only RepositoryId while the entity
        // still has its temporary zero key causes SQLite FK failures.
        await db.SaveChangesWithRetryAsync();
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-7);
        if (await db.FeedItems.AnyAsync(f => f.RepositoryId == repo.Id && f.Source == (int)source && f.DiscoveredAt >= cutoff)) return false;
        await NormalizeRepositoryFacetsAsync(db, repo, repository);
        var batchId = string.IsNullOrWhiteSpace(reason.BatchId) ? $"{source}:{now:yyyyMMdd}" : reason.BatchId;
        db.FeedItems.Add(new FeedItemEntity { RepositoryId = repo.Id, Source = (int)source, Reason = reason.Summary, MatchedRule = reason.MatchedRule, Score = reason.Score, CoarseScore = reason.CoarseScore, FineScore = reason.Score, BatchId = batchId, IsExploration = reason.IsExploration, DiscoveredAt = now });
        await db.SaveChangesWithRetryAsync(); return true;
    }

    public async Task MarkReadAsync(long id, bool dismissed = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var item = await db.FeedItems.FindAsync(id);
        if (item != null) { item.IsRead = true; item.IsDismissed = dismissed; await db.SaveChangesWithRetryAsync(); }
    }

    public async Task<IReadOnlyList<Repository>> GetSavedRepositoriesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Bookmarks.AsNoTracking().Include(b => b.Repository).ToListAsync();
        return rows.OrderByDescending(b => b.BookmarkedAt).Select(b => MapRepository(b.Repository)).ToList();
    }

    public async Task ToggleSavedAsync(long repositoryId, string collection = "Library", string? note = null, IEnumerable<string>? tags = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var saved = await db.Bookmarks.Include(b => b.Tags).FirstOrDefaultAsync(b => b.RepositoryId == repositoryId);
        if (saved != null) { db.Bookmarks.Remove(saved); var old = await db.Repositories.FindAsync(repositoryId); if (old != null) old.IsBookmarked = false; await db.SaveChangesWithRetryAsync(); return; }
        saved = new BookmarkEntity { RepositoryId = repositoryId, BookmarkedAt = DateTimeOffset.UtcNow, CollectionName = collection, Notes = note };
        foreach (var tag in tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase) ?? []) saved.Tags.Add(new BookmarkTagEntity { Name = tag.Trim() });
        db.Bookmarks.Add(saved); var repository = await db.Repositories.FindAsync(repositoryId); if (repository != null) repository.IsBookmarked = true; await db.SaveChangesWithRetryAsync();
    }

    public async Task<IReadOnlyList<Repository>> GetRepositoryCandidatesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return (await db.Repositories.AsNoTracking().Where(r => !r.IsIgnored).OrderByDescending(r => r.DiscoveryScore).Take(100).ToListAsync()).Select(MapRepository).ToList();
    }
    public Task<IReadOnlyList<Repository>> GetSavedForReleaseChecksAsync() => GetSavedRepositoriesAsync();

    public async Task<bool> TryRecordReleaseAsync(long repositoryId, ReleaseInfo release)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.ReleaseNotifications.AnyAsync(r => r.RepositoryId == repositoryId && r.ReleaseId == release.Id)) return false;
        db.ReleaseNotifications.Add(new ReleaseNotificationEntity { RepositoryId = repositoryId, ReleaseId = release.Id, PublishedAt = release.PublishedAt }); await db.SaveChangesWithRetryAsync(); return true;
    }

    private static async Task<RepositoryEntity> UpsertRepositoryAsync(RepoGalaxyDbContext db, Repository model)
    {
        var entity = await db.Repositories.FirstOrDefaultAsync(r => r.Owner == model.Owner && r.Name == model.Name);
        if (entity == null) { entity = new RepositoryEntity { Owner = model.Owner, Name = model.Name }; db.Repositories.Add(entity); }
        entity.GitHubId = model.GitHubId; entity.Description = model.Description; entity.PrimaryLanguage = model.PrimaryLanguage; entity.HtmlUrl = model.HtmlUrl; entity.IsPrivate = model.IsPrivate; entity.IsArchived = model.IsArchived; entity.Stars = model.Stars; entity.Forks = model.Forks; entity.Watchers = model.Watchers; entity.OpenIssues = model.OpenIssues; entity.CreatedAt = model.CreatedAt; entity.UpdatedAt = model.UpdatedAt; entity.LastPushedAt = model.LastPushedAt; entity.DiscoveryScore = model.DiscoveryScore; entity.TopicsJson = JsonSerializer.Serialize(model.Topics); entity.LanguagesJson = JsonSerializer.Serialize(model.Languages); entity.CachedAt = DateTimeOffset.UtcNow;
        return entity;
    }

    private static async Task NormalizeRepositoryFacetsAsync(RepoGalaxyDbContext db, RepositoryEntity entity, Repository model)
    {
        await db.RepositoryTopics.Where(x => x.RepositoryId == entity.Id).ExecuteDeleteAsync();
        await db.RepositoryLanguages.Where(x => x.RepositoryId == entity.Id).ExecuteDeleteAsync();
        foreach (var topic in model.Topics.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            db.RepositoryTopics.Add(new RepositoryTopicEntity { RepositoryId = entity.Id, Topic = topic.Trim().ToLowerInvariant() });
        foreach (var language in model.Languages.Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.First()))
            db.RepositoryLanguages.Add(new RepositoryLanguageEntity { RepositoryId = entity.Id, Language = language.Name, Bytes = language.Bytes, Percentage = language.Percentage });
        if (model.Languages.Count == 0 && !string.IsNullOrWhiteSpace(model.PrimaryLanguage))
            db.RepositoryLanguages.Add(new RepositoryLanguageEntity { RepositoryId = entity.Id, Language = model.PrimaryLanguage, Percentage = 1 });
    }

    private static DiscoverySubscription Map(DiscoverySubscriptionEntity x) => new() { Id = x.Id, Name = x.Name, Topics = Read(x.TopicsJson), Languages = Read(x.LanguagesJson), Keywords = Read(x.KeywordsJson), IsEnabled = x.IsEnabled, NotificationThreshold = x.NotificationThreshold, LastSyncedAt = x.LastSyncedAt };
    private static FeedItem Map(FeedItemEntity x) => new() { Id = x.Id, RepositoryId = x.RepositoryId, Repository = MapRepository(x.Repository), Source = (FeedSource)x.Source, Reason = new FeedReason { Summary = x.Reason, MatchedRule = x.MatchedRule, Score = x.FineScore, CoarseScore = x.CoarseScore, BatchId = x.BatchId, IsExploration = x.IsExploration }, DiscoveredAt = x.DiscoveredAt, IsRead = x.IsRead, IsDismissed = x.IsDismissed };
    private static Repository MapRepository(RepositoryEntity x) => new() { Id = x.Id, GitHubId = x.GitHubId, Owner = x.Owner, Name = x.Name, Description = x.Description ?? string.Empty, PrimaryLanguage = x.PrimaryLanguage ?? string.Empty, HtmlUrl = x.HtmlUrl ?? string.Empty, IsPrivate = x.IsPrivate, IsArchived = x.IsArchived, Stars = x.Stars, Forks = x.Forks, Watchers = x.Watchers, OpenIssues = x.OpenIssues, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt, LastPushedAt = x.LastPushedAt, DiscoveryScore = x.DiscoveryScore, Topics = Read(x.TopicsJson), CachedAt = x.CachedAt };
    private static List<string> Read(string? value) { try { return JsonSerializer.Deserialize<List<string>>(value ?? "[]") ?? []; } catch { return []; } }
}
