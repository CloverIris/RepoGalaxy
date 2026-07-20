using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.Services;

namespace RepoGalaxy.Recommendation.Services;

/// <summary>Rate-friendly incremental discovery synchronizer for the active desktop session.</summary>
public sealed class DiscoverySyncService : IDisposable
{
    private readonly IGitHubClient _github;
    private readonly DiscoveryStore _store;
    private Timer? _timer;
    private bool _running;
    public event EventHandler<FeedItem>? NewFeedItem;

    public DiscoverySyncService(IGitHubClient github, DiscoveryStore store) { _github = github; _store = store; }

    public void Start(TimeSpan? interval = null)
    {
        if (_running) return;
        _running = true;
        _timer = new Timer(async _ => await SyncAsync(), null, TimeSpan.FromSeconds(10), interval ?? TimeSpan.FromMinutes(30));
    }

    public async Task SyncAsync()
    {
        if (!_running) return;
        foreach (var subscription in (await _store.GetSubscriptionsAsync()).Where(s => s.IsEnabled))
        {
            var query = BuildQuery(subscription);
            if (string.IsNullOrWhiteSpace(query)) continue;
            var results = await _github.SearchRepositoriesAsync(query, subscription.Languages.FirstOrDefault(), "updated");
            foreach (var repo in results.Take(30))
            {
                repo.CalculateDiscoveryScore();
                var reason = new FeedReason { Summary = $"匹配订阅「{subscription.Name}」", MatchedRule = query, Score = repo.DiscoveryScore };
                if (await _store.AddFeedItemAsync(repo, FeedSource.Subscription, reason) && repo.DiscoveryScore >= subscription.NotificationThreshold)
                    NewFeedItem?.Invoke(this, new FeedItem { Repository = repo, Source = FeedSource.Subscription, Reason = reason, DiscoveredAt = DateTimeOffset.UtcNow });
            }
            subscription.LastSyncedAt = DateTimeOffset.UtcNow;
            await _store.SaveSubscriptionAsync(subscription);
        }

        var trending = await _github.GetTrendingAsync();
        foreach (var repo in trending.Take(30))
        {
            repo.CalculateDiscoveryScore();
            await _store.AddFeedItemAsync(repo, FeedSource.Trending, new FeedReason { Summary = "近期热门项目", Score = repo.DiscoveryScore });
        }

        foreach (var repo in await _store.GetSavedForReleaseChecksAsync())
        {
            var release = await _github.GetLatestReleaseAsync(repo.Owner, repo.Name);
            if (release != null && await _store.TryRecordReleaseAsync(repo.Id, release))
            {
                var reason = new FeedReason { Summary = $"发布正式版本 {release.TagName}", Score = 1 };
                if (await _store.AddFeedItemAsync(repo, FeedSource.Release, reason))
                    NewFeedItem?.Invoke(this, new FeedItem { Repository = repo, Source = FeedSource.Release, Reason = reason, DiscoveredAt = DateTimeOffset.UtcNow });
            }
        }
    }

    public async Task RefreshForYouAsync()
    {
        foreach (var repo in await _store.GetRepositoryCandidatesAsync())
        {
            var reason = new FeedReason { Summary = "基于你的阅读与收藏偏好", Score = repo.DiscoveryScore };
            await _store.AddFeedItemAsync(repo, FeedSource.ForYou, reason);
        }
    }

    public static string BuildQuery(DiscoverySubscription subscription)
    {
        var terms = new List<string>();
        terms.AddRange(subscription.Keywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => $"\"{x.Trim()}\""));
        terms.AddRange(subscription.Topics.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => $"topic:{x.Trim()}"));
        if (subscription.Languages.Count > 0) terms.Add($"language:{subscription.Languages[0].Trim()}");
        return string.Join(' ', terms);
    }

    public void Dispose() { _running = false; _timer?.Dispose(); }
}
