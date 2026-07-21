using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RepoGalaxy.Data.Entities;

[Table("Repositories")]
public class RepositoryEntity
{
    [Key] public long Id { get; set; }
    [Required] public string GitHubId { get; set; } = string.Empty;
    [Required] public string Owner { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PrimaryLanguage { get; set; }
    public string? TopicsJson { get; set; }
    public string? HtmlUrl { get; set; }
    public string? OwnerAvatarUrl { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsArchived { get; set; }
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int Watchers { get; set; }
    public int OpenIssues { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
    public double DiscoveryScore { get; set; }
    public bool IsBookmarked { get; set; }
    public bool IsIgnored { get; set; }
    public DateTimeOffset? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
    public DateTimeOffset CachedAt { get; set; }
    public string? LanguagesJson { get; set; }
    public ICollection<BookmarkEntity> Bookmarks { get; set; } = new List<BookmarkEntity>();
    public ICollection<ViewHistoryEntity> ViewHistories { get; set; } = new List<ViewHistoryEntity>();
    public ICollection<FeedItemEntity> FeedItems { get; set; } = new List<FeedItemEntity>();
}

[Table("Bookmarks")]
public class BookmarkEntity
{
    [Key] public long Id { get; set; }
    public long RepositoryId { get; set; }
    public DateTimeOffset BookmarkedAt { get; set; }
    public string CollectionName { get; set; } = "Library";
    public string? Notes { get; set; }
    public int Priority { get; set; }
    [ForeignKey(nameof(RepositoryId))] public RepositoryEntity Repository { get; set; } = null!;
    public ICollection<BookmarkTagEntity> Tags { get; set; } = new List<BookmarkTagEntity>();
}

[Table("BookmarkTags")]
public class BookmarkTagEntity
{
    [Key] public long Id { get; set; }
    public long BookmarkId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [ForeignKey(nameof(BookmarkId))] public BookmarkEntity Bookmark { get; set; } = null!;
}

[Table("ViewHistories")]
public class ViewHistoryEntity
{
    [Key] public long Id { get; set; }
    public long RepositoryId { get; set; }
    public DateTimeOffset ViewedAt { get; set; }
    public long DurationSeconds { get; set; }
    public int Source { get; set; }
    public string? ReferrerTopic { get; set; }
    [ForeignKey(nameof(RepositoryId))] public RepositoryEntity Repository { get; set; } = null!;
}

[Table("DiscoverySubscriptions")]
public class DiscoverySubscriptionEntity
{
    [Key] public long Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    public string TopicsJson { get; set; } = "[]";
    public string LanguagesJson { get; set; } = "[]";
    public string KeywordsJson { get; set; } = "[]";
    public bool IsEnabled { get; set; } = true;
    public double NotificationThreshold { get; set; } = 0.75;
    public DateTimeOffset? LastSyncedAt { get; set; }
}

[Table("FeedItems")]
public class FeedItemEntity
{
    [Key] public long Id { get; set; }
    public long RepositoryId { get; set; }
    public int Source { get; set; }
    [Required] public string Reason { get; set; } = string.Empty;
    public string? MatchedRule { get; set; }
    public double Score { get; set; }
    public double CoarseScore { get; set; }
    public double FineScore { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public bool IsExploration { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsDismissed { get; set; }
    public bool NotificationDelivered { get; set; }
    [ForeignKey(nameof(RepositoryId))] public RepositoryEntity Repository { get; set; } = null!;
}

[Table("ReleaseNotifications")]
public class ReleaseNotificationEntity
{
    [Key] public long Id { get; set; }
    public long RepositoryId { get; set; }
    public long ReleaseId { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}

[Table("Users")]
public class UserEntity
{
    [Key] public long Id { get; set; }
    public string GitHubId { get; set; } = string.Empty;
    [Required] public string Login { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? Blog { get; set; }
    public string? TwitterUsername { get; set; }
    public string? ProfileUrl { get; set; }
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
[Table("LocalRepositories")]
public class LocalRepositoryEntity { [Key] public long Id { get; set; } [Required] public string Name { get; set; } = string.Empty; [Required] public string LocalPath { get; set; } = string.Empty; public string? GitHubUrl { get; set; } public bool IsTracked { get; set; } public DateTimeOffset AddedAt { get; set; } }
[Table("UserPreferences")]
public class UserPreferenceEntity { [Key] public long Id { get; set; } public long UserId { get; set; } public string? InterestedTopicsJson { get; set; } public string? InterestedLanguagesJson { get; set; } public int MinStarsThreshold { get; set; } public int MaxStarsThreshold { get; set; } = 1000000; public string? IgnoredTopicsJson { get; set; } public bool PreferFreshContent { get; set; } = true; public bool IncludeTrending { get; set; } = true; public bool PreferSmallProjects { get; set; } public bool DarkMode { get; set; } public int FeedPageSize { get; set; } = 50; public int MaxCacheSizeGB { get; set; } = 2; public bool AutoCleanCache { get; set; } = true; public bool? UseSystemTheme { get; set; } = true; public int SyncIntervalMinutes { get; set; } = 30; public double NotificationThreshold { get; set; } = .75; public int MemoryCacheSizeMB { get; set; } = 256; public int PersistentCacheSizeMB { get; set; } = 1024; public int FeedCacheTtlMinutes { get; set; } = 30; public int DetailCacheTtlMinutes { get; set; } = 360; public int NewsCacheTtlMinutes { get; set; } = 30; public string CachePreset { get; set; } = "均衡"; public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow; }
