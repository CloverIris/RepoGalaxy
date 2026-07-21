using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Data.Services;
using RepoGalaxy.GitHub.Services;

namespace RepoGalaxy.Recommendation.Services;

public sealed class DiscoverySyncService : IDisposable
{
    private readonly IGitHubClient _github; private readonly DiscoveryStore _store; private readonly RepositoryService _repositories; private readonly IRecommendationEngine _recommendations; private readonly IDbContextFactory<RepoGalaxyDbContext> _factory; private readonly GitHubRequestBudget _budget;
    private readonly SemaphoreSlim _gate = new(1, 1); private readonly GuestSessionRequestPolicy _guestPolicy = new(); private CancellationTokenSource? _lifetime; private Task? _loop; private bool _authenticated; private string _accountId = string.Empty; private TimeSpan _interval = TimeSpan.FromMinutes(30);
    private CancellationTokenSource _scheduleChanged = new();
    public event EventHandler<FeedItem>? NewFeedItem; public event EventHandler<string>? StatusChanged;
    public DiscoverySyncService(IGitHubClient github, DiscoveryStore store, RepositoryService repositories, IRecommendationEngine recommendations, IDbContextFactory<RepoGalaxyDbContext> factory, GitHubRequestBudget budget) { _github = github; _store = store; _repositories = repositories; _recommendations = recommendations; _factory = factory; _budget = budget; }

    public void Start(bool authenticated, TimeSpan? interval = null) => _ = StartAsync(authenticated, interval: interval);
    public void UpdateInterval(TimeSpan? interval)
    {
        _interval = interval ?? Timeout.InfiniteTimeSpan;
        var previous = Interlocked.Exchange(ref _scheduleChanged, new CancellationTokenSource());
        previous.Cancel();
    }
    public async Task StartAsync(bool authenticated, string accountId = "", TimeSpan? interval = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await StopAsync();
        _authenticated = authenticated; _accountId = accountId; if (interval.HasValue) _interval = interval.Value;
        _lifetime = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_lifetime.Token), CancellationToken.None);
    }
    public async Task StopAsync()
    {
        if (_lifetime is null) return;
        _lifetime.Cancel();
        try
        {
            // StopAsync is also used from the desktop lifetime's synchronous Dispose path.
            // Never capture Avalonia's UI synchronization context here or shutdown deadlocks.
            if (_loop is not null) await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        _lifetime.Dispose();
        _lifetime = null;
        _loop = null;
    }
    private async Task RunAsync(CancellationToken ct)
    {
        if (_authenticated) await BootstrapAccountAsync(ct); else await SyncAsync(false, ct);
        if (!_authenticated) return;
        while (!ct.IsCancellationRequested)
        {
            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _scheduleChanged.Token);
            try { await Task.Delay(_interval == Timeout.InfiniteTimeSpan ? Timeout.InfiniteTimeSpan : _interval, delayCancellation.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { continue; }
            await SyncAsync(false, ct);
        }
    }
    public Task SyncAsync(bool manual = false) => SyncAsync(manual, _lifetime?.Token ?? CancellationToken.None);
    public async Task SyncAsync(bool manual, CancellationToken ct)
    {
        await _gate.WaitAsync(ct); var run = await BeginRunAsync("Discovery", ct);
        try
        {
            if (!_authenticated)
            {
                if (!_guestPolicy.TryConsume(manual)) { StatusChanged?.Invoke(this, "游客模式正在使用本地缓存。"); await CompleteRunAsync(run, null, ct); return; }
                var guestAdded = await SyncTrendingAsync(manual, ct); StatusChanged?.Invoke(this, guestAdded == 0
                    ? "游客热门数据已核对，本地内容仍是最新状态。"
                    : $"游客热门 Feed 新增 {guestAdded} 个项目；后续仅手动刷新会访问 GitHub。"); await CompleteRunAsync(run, null, ct); return;
            }
            if (!_budget.CanSearch(out var resetAt)) { StatusChanged?.Invoke(this, $"GitHub 搜索额度已暂停，将在 {resetAt.LocalDateTime:t} 后恢复。"); await CompleteRunAsync(run, "search_budget", ct); return; }
            foreach (var subscription in (await _store.GetSubscriptionsAsync()).Where(x => x.IsEnabled))
            {
                ct.ThrowIfCancellationRequested(); if (!_budget.CanSearch(out _)) break; var query = BuildQuery(subscription); if (string.IsNullOrWhiteSpace(query)) continue;
                var next = await GetCheckpointAsync("Subscription", subscription.Id.ToString(), ct);
                for (var pageNumber = 0; pageNumber < 2 && _budget.CanSearch(out _); pageNumber++)
                {
                    var page = await _github.SearchRepositoriesPageAsync(query, subscription.Languages.FirstOrDefault(), "updated", next, ct);
                    foreach (var repo in page.Items) { repo.CalculateDiscoveryScore(); var reason = new FeedReason { Summary = $"匹配订阅：{subscription.Name}", MatchedRule = query, Score = repo.DiscoveryScore }; if (await _store.AddFeedItemAsync(repo, FeedSource.Subscription, reason) && repo.DiscoveryScore >= subscription.NotificationThreshold) NewFeedItem?.Invoke(this, NewItem(repo, FeedSource.Subscription, reason)); }
                    next = page.NextPageUrl;
                    await SaveCheckpointAsync("Subscription", subscription.Id.ToString(), next, ct);
                    if (next is null) break;
                }
                subscription.LastSyncedAt = DateTimeOffset.UtcNow; await _store.SaveSubscriptionAsync(subscription);
            }
            var trendingAdded = _budget.CanSearch(out _) ? await SyncTrendingAsync(manual, ct) : 0;
            await CheckSavedReleasesAsync(ct); await RefreshForYouAsync(ct); await CompleteRunAsync(run, null, ct);
            StatusChanged?.Invoke(this, trendingAdded == 0 ? "同步完成 · 本地 Feed 已是最新状态。" : $"同步完成 · 热门 Feed 新增 {trendingAdded} 个项目。");
        }
        catch (OperationCanceledException) { await CompleteRunAsync(run, "cancelled", CancellationToken.None); throw; }
        catch { await CompleteRunAsync(run, "sync_failed", CancellationToken.None); StatusChanged?.Invoke(this, "同步失败，已保留本地缓存并保存恢复位置。"); }
        finally { _gate.Release(); }
    }
    private async Task BootstrapAccountAsync(CancellationToken ct)
    {
        StatusChanged?.Invoke(this, "正在后台同步个人仓库…"); await SyncRelationPagesAsync("Owned", (next, token) => _github.GetUserRepositoriesPageAsync(next, token), ct);
        StatusChanged?.Invoke(this, "正在后台同步 Star…"); await SyncRelationPagesAsync("Starred", (next, token) => _github.GetStarredRepositoriesPageAsync(next, token), ct);
        await SyncAsync(false, ct);
    }
    private async Task SyncRelationPagesAsync(string relation, Func<string?, CancellationToken, Task<GitHubPage<Repository>>> loader, CancellationToken ct)
    {
        string? next = await GetCheckpointAsync(relation, ct); do
        {
            ct.ThrowIfCancellationRequested(); var page = await loader(next, ct); if (page.Items.Count > 0) await _repositories.SaveRepositoriesAsync(page.Items);
            await using var db = await _factory.CreateDbContextAsync(ct); await using var transaction = await db.Database.BeginTransactionAsync(ct);
            foreach (var model in page.Items) { var repo = await db.Repositories.FirstAsync(x => x.GitHubId == model.GitHubId, ct); var item = await db.UserRepositoryRelations.FirstOrDefaultAsync(x => x.AccountId == _accountId && x.RepositoryId == repo.Id && x.Relation == relation, ct); if (item is null) db.Add(new UserRepositoryRelationEntity { AccountId = _accountId, RepositoryId = repo.Id, Relation = relation, IsPrivate = model.IsPrivate, RelatedAt = DateTimeOffset.UtcNow }); }
            await SaveCheckpointAsync(db, relation, page.NextPageUrl, ct); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); next = page.NextPageUrl;
        } while (next is not null);
    }
    private async Task<int> SyncTrendingAsync(bool manual, CancellationToken ct)
    {
        var date = DateTime.UtcNow.Date;
        var scope = $"daily:{date:yyyyMMdd}";
        var next = await GetCheckpointAsync("Trending", scope, ct);
        var pages = manual && _authenticated ? 2 : 1;
        var added = 0;
        for (var pageNumber = 0; pageNumber < pages && _budget.CanSearch(out _); pageNumber++)
        {
            var query = $"pushed:>{date.AddDays(-1):yyyy-MM-dd} stars:>100 archived:false fork:false";
            var page = await _github.SearchRepositoriesPageAsync(query, sort: "stars", nextPageUrl: next, cancellationToken: ct);
            foreach (var repo in page.Items)
            {
                ct.ThrowIfCancellationRequested();
                repo.CalculateDiscoveryScore();
                var reason = new FeedReason { Summary = "近期热门项目", Score = repo.DiscoveryScore };
                if (await _store.AddFeedItemAsync(repo, FeedSource.Trending, reason))
                {
                    added++;
                    if (_authenticated) NewFeedItem?.Invoke(this, NewItem(repo, FeedSource.Trending, reason));
                }
            }
            next = page.NextPageUrl;
            await SaveCheckpointAsync("Trending", scope, next, ct);
            if (next is null) break;
        }
        return added;
    }
    private async Task CheckSavedReleasesAsync(CancellationToken ct)
    {
        var saved = (await _store.GetSavedForReleaseChecksAsync()).OrderBy(x => x.Id).ToList();
        if (saved.Count == 0) return;
        var rawCursor = await GetCheckpointAsync("Release", "rotation", ct);
        var offset = int.TryParse(rawCursor, out var parsed) ? Math.Clamp(parsed, 0, saved.Count - 1) : 0;
        foreach (var repo in saved.Skip(offset).Concat(saved.Take(offset)).Take(20))
        {
            ct.ThrowIfCancellationRequested(); var release = await _github.GetLatestReleaseAsync(repo.Owner, repo.Name, ct); if (release is not null && await _store.TryRecordReleaseAsync(repo.Id, release)) { var reason = new FeedReason { Summary = $"发布正式版本 {release.TagName}", Score = 1 }; if (await _store.AddFeedItemAsync(repo, FeedSource.Release, reason)) NewFeedItem?.Invoke(this, NewItem(repo, FeedSource.Release, reason)); }
        }
        await SaveCheckpointAsync("Release", "rotation", ((offset + Math.Min(20, saved.Count)) % saved.Count).ToString(), ct);
    }
    public Task RefreshForYouAsync() => RefreshForYouAsync(CancellationToken.None);
    private async Task RefreshForYouAsync(CancellationToken ct)
    {
        foreach (var ranked in await _recommendations.GetRankedRecommendationsAsync(60))
        {
            ct.ThrowIfCancellationRequested();
            var result = ranked.Result;
            await _store.AddFeedItemAsync(result.Repository, FeedSource.ForYou, new FeedReason { Summary = ranked.Explanation.Summary, Score = result.FineScore, CoarseScore = result.CoarseScore, BatchId = ranked.BatchId, IsExploration = result.IsExploration, Signals = ranked.Explanation.Signals });
        }
    }
    private async Task<long> BeginRunAsync(string type, CancellationToken ct) { await using var db = await _factory.CreateDbContextAsync(ct); var item = new SyncRunEntity { CorrelationId = Guid.NewGuid().ToString("N"), JobType = type, AccountId = _accountId, State = "Running", StartedAt = DateTimeOffset.UtcNow }; db.Add(item); await db.SaveChangesAsync(ct); return item.Id; }
    private async Task CompleteRunAsync(long id, string? error, CancellationToken ct) { await using var db = await _factory.CreateDbContextAsync(ct); var item = await db.SyncRuns.FindAsync([id], ct); if (item is null) return; item.CompletedAt = DateTimeOffset.UtcNow; item.State = error is null ? "Completed" : "Failed"; item.ErrorCode = error; await db.SaveChangesAsync(ct); }
    private async Task<string?> GetCheckpointAsync(string type, CancellationToken ct) { await using var db = await _factory.CreateDbContextAsync(ct); return (await db.SyncCheckpoints.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == _accountId && x.JobType == type && x.ScopeKey == "all", ct))?.NextPageUrl; }
    private async Task<string?> GetCheckpointAsync(string type, string scope, CancellationToken ct) { await using var db = await _factory.CreateDbContextAsync(ct); return (await db.SyncCheckpoints.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == _accountId && x.JobType == type && x.ScopeKey == scope, ct))?.NextPageUrl; }
    private async Task SaveCheckpointAsync(string type, string scope, string? next, CancellationToken ct) { await using var db = await _factory.CreateDbContextAsync(ct); var item = await db.SyncCheckpoints.FirstOrDefaultAsync(x => x.AccountId == _accountId && x.JobType == type && x.ScopeKey == scope, ct); if (item is null) { item = new SyncCheckpointEntity { AccountId = _accountId, JobType = type, ScopeKey = scope }; db.Add(item); } item.NextPageUrl = next; item.UpdatedAt = DateTimeOffset.UtcNow; item.AttemptCount = 0; await db.SaveChangesAsync(ct); }
    private async Task SaveCheckpointAsync(RepoGalaxyDbContext db, string type, string? next, CancellationToken ct) { var item = await db.SyncCheckpoints.FirstOrDefaultAsync(x => x.AccountId == _accountId && x.JobType == type && x.ScopeKey == "all", ct); if (item is null) { item = new SyncCheckpointEntity { AccountId = _accountId, JobType = type, ScopeKey = "all" }; db.Add(item); } item.NextPageUrl = next; item.UpdatedAt = DateTimeOffset.UtcNow; item.AttemptCount = 0; }
    private static FeedItem NewItem(Repository repo, FeedSource source, FeedReason reason) => new() { Repository = repo, Source = source, Reason = reason, DiscoveredAt = DateTimeOffset.UtcNow };
    public static string BuildQuery(DiscoverySubscription subscription) { var terms = new List<string>(); terms.AddRange(subscription.Keywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => $"\"{x.Trim()}\"")); terms.AddRange(subscription.Topics.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => $"topic:{x.Trim()}")); if (subscription.Languages.Count > 0) terms.Add($"language:{subscription.Languages[0].Trim()}"); return string.Join(' ', terms); }
    public void Dispose() { StopAsync().GetAwaiter().GetResult(); _scheduleChanged.Cancel(); _scheduleChanged.Dispose(); _gate.Dispose(); }
}
