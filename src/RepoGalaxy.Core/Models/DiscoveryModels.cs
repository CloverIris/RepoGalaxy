namespace RepoGalaxy.Core.Models;

public enum FeedSource { ForYou, Subscription, Trending, Release }
public sealed class FeedReason { public string Summary { get; set; } = string.Empty; public string? MatchedRule { get; set; } public double Score { get; set; } }
public sealed class FeedItem { public long Id { get; set; } public long RepositoryId { get; set; } public Repository Repository { get; set; } = new(); public FeedSource Source { get; set; } public FeedReason Reason { get; set; } = new(); public DateTimeOffset DiscoveredAt { get; set; } public bool IsRead { get; set; } public bool IsDismissed { get; set; } }
public sealed class DiscoverySubscription { public long Id { get; set; } public string Name { get; set; } = string.Empty; public List<string> Topics { get; set; } = new(); public List<string> Languages { get; set; } = new(); public List<string> Keywords { get; set; } = new(); public bool IsEnabled { get; set; } = true; public double NotificationThreshold { get; set; } = 0.75; public DateTimeOffset? LastSyncedAt { get; set; } }
public sealed class ReleaseInfo { public long Id { get; set; } public string TagName { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string HtmlUrl { get; set; } = string.Empty; public DateTimeOffset PublishedAt { get; set; } }
