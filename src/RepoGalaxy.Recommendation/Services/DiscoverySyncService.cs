using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.Services;
using RepoGalaxy.GitHub.Services;

namespace RepoGalaxy.Recommendation.Services;

/// <summary>Session-bound discovery sync with a strict one-request anonymous mode.</summary>
public sealed class DiscoverySyncService : IDisposable
{
    private readonly IGitHubClient _github;
    private readonly DiscoveryStore _store;
    private readonly GitHubRequestBudget _budget;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Timer? _timer;
    private bool _started;
    private bool _authenticated;
    private readonly GuestSessionRequestPolicy _guestPolicy = new();

    public event EventHandler<FeedItem>? NewFeedItem;
    public event EventHandler<string>? StatusChanged;

    public DiscoverySyncService(IGitHubClient github, DiscoveryStore store, GitHubRequestBudget budget)
    {
        _github = github;
        _store = store;
        _budget = budget;
    }

    public void Start(bool authenticated, TimeSpan? interval = null)
    {
        _authenticated = authenticated;
        if (_started)
        {
            if (authenticated && _timer == null) _timer = new Timer(async _ => await SyncAsync(), null, TimeSpan.Zero, interval ?? TimeSpan.FromMinutes(30));
            return;
        }
        _started = true;
        if (authenticated)
            _timer = new Timer(async _ => await SyncAsync(), null, TimeSpan.FromSeconds(5), interval ?? TimeSpan.FromMinutes(30));
        else
            _ = SyncAsync();
    }

    public async Task SyncAsync(bool manual = false)
    {
        if (!_started) return;
        await _gate.WaitAsync();
        try
        {
            if (!_authenticated)
            {
                if (!_guestPolicy.TryConsume(manual))
                {
                    StatusChanged?.Invoke(this, "游客模式正在使用本地缓存。");
                    return;
                }

                await SyncTrendingAsync();
                StatusChanged?.Invoke(this, "已完成一次游客热门内容获取；后续仅在手动刷新时请求 GitHub。");
                return;
            }

            _budget.Update(await _github.GetRateLimitAsync());
            if (!_budget.CanSearch(out var resetAt))
            {
                StatusChanged?.Invoke(this, $"GitHub 搜索额度已用尽，将在 {resetAt.LocalDateTime:t} 后恢复。");
                return;
            }

            foreach (var subscription in (await _store.GetSubscriptionsAsync()).Where(s => s.IsEnabled))
            {
                if (!_budget.CanSearch(out resetAt)) break;
                var query = BuildQuery(subscription);
                if (string.IsNullOrWhiteSpace(query)) continue;
                var results = await _github.SearchRepositoriesAsync(query, subscription.Languages.FirstOrDefault(), "updated");
                foreach (var repo in results.Take(30))
                {
                    repo.CalculateDiscoveryScore();
                    var reason = new FeedReason { Summary = $"匹配订阅：{subscription.Name}", MatchedRule = query, Score = repo.DiscoveryScore };
                    if (await _store.AddFeedItemAsync(repo, FeedSource.Subscription, reason) && repo.DiscoveryScore >= subscription.NotificationThreshold)
                        NewFeedItem?.Invoke(this, NewItem(repo, FeedSource.Subscription, reason));
                }
                subscription.LastSyncedAt = DateTimeOffset.UtcNow;
                await _store.SaveSubscriptionAsync(subscription);
                _budget.Update(await _github.GetRateLimitAsync());
            }

            if (_budget.CanSearch(out _)) await SyncTrendingAsync();
            await CheckSavedReleasesAsync();
            await RefreshForYouAsync();
            StatusChanged?.Invoke(this, "同步完成。");
        }
        catch (Exception)
        {
            StatusChanged?.Invoke(this, "同步失败，已保留本地缓存；请稍后重试。");
        }
        finally { _gate.Release(); }
    }

    private async Task SyncTrendingAsync()
    {
        foreach (var repo in (await _github.GetTrendingAsync()).Take(30))
        {
            repo.CalculateDiscoveryScore();
            var reason = new FeedReason { Summary = "近期热门项目", Score = repo.DiscoveryScore };
            if (await _store.AddFeedItemAsync(repo, FeedSource.Trending, reason) && _authenticated)
                NewFeedItem?.Invoke(this, NewItem(repo, FeedSource.Trending, reason));
        }
        if (_authenticated) _budget.Update(await _github.GetRateLimitAsync());
    }

    private async Task CheckSavedReleasesAsync()
    {
        foreach (var repo in await _store.GetSavedForReleaseChecksAsync())
        {
            var release = await _github.GetLatestReleaseAsync(repo.Owner, repo.Name);
            if (release != null && await _store.TryRecordReleaseAsync(repo.Id, release))
            {
                var reason = new FeedReason { Summary = $"发布正式版本 {release.TagName}", Score = 1 };
                if (await _store.AddFeedItemAsync(repo, FeedSource.Release, reason)) NewFeedItem?.Invoke(this, NewItem(repo, FeedSource.Release, reason));
            }
        }
    }

    public async Task RefreshForYouAsync()
    {
        foreach (var repo in await _store.GetRepositoryCandidatesAsync())
            await _store.AddFeedItemAsync(repo, FeedSource.ForYou, new FeedReason { Summary = "基于你的阅读与收藏偏好", Score = repo.DiscoveryScore });
    }

    private static FeedItem NewItem(Repository repo, FeedSource source, FeedReason reason) => new() { Repository = repo, Source = source, Reason = reason, DiscoveredAt = DateTimeOffset.UtcNow };
    public static string BuildQuery(DiscoverySubscription subscription)
    {
        var terms = new List<string>();
        terms.AddRange(subscription.Keywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => $"\"{x.Trim()}\""));
        terms.AddRange(subscription.Topics.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => $"topic:{x.Trim()}"));
        if (subscription.Languages.Count > 0) terms.Add($"language:{subscription.Languages[0].Trim()}");
        return string.Join(' ', terms);
    }
    public void Dispose() { _timer?.Dispose(); _gate.Dispose(); }
}
