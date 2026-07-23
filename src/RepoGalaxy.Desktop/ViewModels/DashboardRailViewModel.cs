using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.GitHub.Services;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed class DashboardListViewModel
{
    public DashboardListViewModel(string title, string subtitle, IEnumerable<DashboardListItem> items) { Title = title; Subtitle = subtitle; Items = new(items); }
    public string Title { get; }
    public string Subtitle { get; }
    public ObservableCollection<DashboardListItem> Items { get; }
    public bool IsEmpty => Items.Count == 0;
    public string EmptyText => Title == "本地相关" ? "添加本地仓库后生成" : "正在积累有效数据";
}

public sealed record ContributionCell(DateOnly Date, int Count, string Unit)
{
    public double Opacity => Count switch { 0 => .08, 1 => .25, <= 3 => .48, <= 6 => .72, _ => 1 };
    public string ToolTip => $"{Date:yyyy-MM-dd} · {Count} 次{Unit}";
}

public sealed partial class DashboardRailViewModel : ViewModelBase
{
    private readonly DashboardDataService _data;
    private readonly IExternalLinkService _links;
    private readonly ICacheService _cache;
    private readonly IGitHubQuotaService _quota;
    private readonly ILogger<DashboardRailViewModel> _logger;
    private readonly DispatcherTimer _quotaTimer;
    public ObservableCollection<DashboardListViewModel> TopLists { get; } = [];
    public ObservableCollection<ContributionCell> Contributions { get; } = [];
    public ObservableCollection<NewsArticle> News { get; } = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = "正在读取本地概览";
    [ObservableProperty] private int _streak;
    [ObservableProperty] private int _weekCommits;
    [ObservableProperty] private int _totalContributions;
    [ObservableProperty] private string _contributionSourceText = "正在确定数据来源";
    [ObservableProperty] private string _contributionObservedText = string.Empty;
    [ObservableProperty] private ContributionDataSource _contributionSource;
    [ObservableProperty] private ContributionLoadState _contributionLoadState;
    [ObservableProperty] private bool _isContributionRefreshing;
    [ObservableProperty] private string _cacheHealth = "本地缓存就绪";
    [ObservableProperty] private double _coreBudgetRatio;
    [ObservableProperty] private double _searchBudgetRatio;
    [ObservableProperty] private double _graphQlBudgetRatio;
    [ObservableProperty] private string _coreBudgetText = "等待首次 Core 请求";
    [ObservableProperty] private string _searchBudgetText = "等待首次 Search 请求";
    [ObservableProperty] private string _graphQlBudgetText = "等待首次 GraphQL 请求";
    [ObservableProperty] private string _coreResetText = "尚未观测";
    [ObservableProperty] private string _searchResetText = "尚未观测";
    [ObservableProperty] private string _graphQlResetText = "尚未观测";
    [ObservableProperty] private string _budgetSessionText = "游客额度";
    [ObservableProperty] private string _budgetObservedText = "尚未从 GitHub 校准";
    [ObservableProperty] private bool _isQuotaRefreshing;
    [ObservableProperty] private bool _isSyncing;
    public string CoreBudgetColor => BudgetColor(CoreBudgetRatio);
    public string SearchBudgetColor => BudgetColor(SearchBudgetRatio);
    public string GraphQlBudgetColor => BudgetColor(GraphQlBudgetRatio);
    public bool HasGraphQlBudget => _quota.Snapshot.GraphQl is not null;
    public Func<Task>? SyncCurrentFeedAsync { get; set; }
    public string ContributionUnit => ContributionSource is ContributionDataSource.GitHubFresh or ContributionDataSource.GitHubStale ? "贡献" : "提交";
    public string StreakText => Streak == 0 ? $"今天还没有{ContributionUnit}" : $"连续贡献 {Streak} 天";
    public string WeekText => $"本周 {WeekCommits} 次{ContributionUnit}";
    public string TotalContributionText => $"近一年 {TotalContributions:N0} 次{ContributionUnit}";
    public DashboardRailViewModel(DashboardDataService data, IExternalLinkService links, ICacheService cache, IGitHubQuotaService quota, ILogger<DashboardRailViewModel> logger)
    {
        _data = data; _links = links; _cache = cache; _quota = quota; _logger = logger;
        _quota.Changed += (_, snapshot) => Dispatcher.UIThread.Post(() => ApplyBudget(snapshot));
        ApplyBudget(_quota.Snapshot);
        _quotaTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _quotaTimer.Tick += (_, _) => UpdateResetCountdowns(_quota.Snapshot);
        _quotaTimer.Start();
    }
    public async Task LoadAsync(bool forceContributions = false)
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            ContributionLoadState = ContributionLoadState.Loading;
            var snapshot = await _data.LoadAsync(forceContributions);
            TopLists.Clear(); TopLists.Add(new("今日增长", "24 小时关注变化", snapshot.Growth)); TopLists.Add(new("本周新秀", "创建 30 天内", snapshot.Rookies)); TopLists.Add(new("本地相关", "匹配你的工作区", snapshot.Local));
            var calendar = snapshot.ContributionCalendar;
            var unit = calendar.Source is ContributionDataSource.GitHubFresh or ContributionDataSource.GitHubStale ? "贡献" : "提交";
            Contributions.Clear(); foreach (var item in calendar.Days) Contributions.Add(new(item.Date, item.Count, unit));
            News.Clear(); foreach (var item in snapshot.Releases.Concat(snapshot.News).Take(7)) News.Add(item);
            ContributionSource = calendar.Source;
            ContributionSourceText = calendar.Source switch
            {
                ContributionDataSource.GitHubFresh => "GitHub 官方贡献",
                ContributionDataSource.GitHubStale => $"GitHub 缓存 · {calendar.FetchedAt.LocalDateTime:MM-dd HH:mm}",
                ContributionDataSource.LocalGit => "本地 Git 贡献",
                _ => "暂无贡献数据"
            };
            ContributionObservedText = calendar.Days.Count == 0
                ? "登录 GitHub 或添加本地仓库后显示"
                : $"更新于 {calendar.FetchedAt.LocalDateTime:MM-dd HH:mm}";
            ContributionLoadState = calendar.Source == ContributionDataSource.GitHubStale
                ? ContributionLoadState.Stale
                : calendar.Days.Count > 0 ? ContributionLoadState.Ready : ContributionLoadState.Unavailable;
            TotalContributions = calendar.TotalContributions;
            Streak = calendar.CurrentStreak;
            WeekCommits = calendar.WeekContributions;
            NotifyContributionText();
            var cache = await _cache.GetStatisticsAsync(); var limit = _quota.Snapshot;
            CacheHealth = limit.Core is null && limit.Search is null
                ? $"缓存 {cache.TotalBytes / 1024d / 1024d:N1} MB · 命中 {cache.HitRate:P0}"
                : $"缓存 {cache.TotalBytes / 1024d / 1024d:N1} MB · Core {(limit.Core?.Remaining ?? 0):N0} · Search {(limit.Search?.Remaining ?? 0):N0}";
            Status = calendar.Source switch
            {
                ContributionDataSource.GitHubFresh => "GitHub 贡献日历已更新",
                ContributionDataSource.GitHubStale => "网络不可用，正在显示 GitHub 缓存",
                ContributionDataSource.LocalGit when calendar.Days.Any(x => x.Count > 0) => "正在显示本地 Git 贡献",
                _ => "添加本地仓库或登录 GitHub 后显示贡献"
            };
        }
        catch (Exception ex) { _logger.LogWarning(ex, "加载工作台概览失败"); ContributionLoadState = ContributionLoadState.Unavailable; Status = "概览暂时离线，稍后会自动重试"; }
        finally { IsLoading = false; }
    }
    [RelayCommand] private void OpenNews(NewsArticle article) { if (!_links.Open(article.Url)) Status = "无法打开该资讯链接"; }

    [RelayCommand]
    private async Task RefreshQuotaAsync()
    {
        if (IsQuotaRefreshing) return;
        IsQuotaRefreshing = true;
        try
        {
            Status = await _quota.RefreshAsync() ? "GitHub 官方额度已更新" : "额度读取失败，继续显示最近观测值";
        }
        finally { IsQuotaRefreshing = false; }
    }

    [RelayCommand]
    private async Task SyncCurrentFeed()
    {
        if (IsSyncing || SyncCurrentFeedAsync is null) return;
        IsSyncing = true;
        try { await SyncCurrentFeedAsync(); }
        finally { IsSyncing = false; }
    }

    [RelayCommand]
    private async Task RefreshContributionsAsync()
    {
        if (IsContributionRefreshing) return;
        IsContributionRefreshing = true;
        try { await LoadAsync(forceContributions: true); }
        finally { IsContributionRefreshing = false; }
    }

    private void ApplyBudget(GitHubBudgetSnapshot snapshot)
    {
        BudgetSessionText = snapshot.SessionKind == GitHubBudgetSessionKind.Authenticated ? "登录额度" : "游客额度";
        CoreBudgetRatio = snapshot.Core?.UsedRatio ?? 0;
        SearchBudgetRatio = snapshot.Search?.UsedRatio ?? 0;
        GraphQlBudgetRatio = snapshot.GraphQl?.UsedRatio ?? 0;
        CoreBudgetText = FormatBudget("Core", snapshot.Core);
        SearchBudgetText = FormatBudget("Search", snapshot.Search);
        GraphQlBudgetText = FormatBudget("GraphQL", snapshot.GraphQl);
        OnPropertyChanged(nameof(HasGraphQlBudget));
        var observed = new[] { snapshot.Core?.ObservedAt, snapshot.Search?.ObservedAt, snapshot.GraphQl?.ObservedAt }
            .Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty().Max();
        BudgetObservedText = observed == default ? "尚未从 GitHub 校准" : $"最后观测 {observed.LocalDateTime:MM-dd HH:mm:ss}";
        UpdateResetCountdowns(snapshot);
    }

    private void UpdateResetCountdowns(GitHubBudgetSnapshot snapshot)
    {
        CoreResetText = FormatReset(snapshot.Core);
        SearchResetText = FormatReset(snapshot.Search);
        GraphQlResetText = FormatReset(snapshot.GraphQl);
    }

    private static string FormatBudget(string name, GitHubRateWindow? window)
        => window is null ? $"{name} · 等待首次请求" : $"{name} · {window.EffectiveUsed:N0} / {window.Limit:N0} · 剩余 {window.Remaining:N0}";
    private static string FormatReset(GitHubRateWindow? window)
    {
        if (window is null) return "尚未观测";
        var target = window.RetryAfter ?? window.ResetAt;
        var remaining = target - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return "等待下一次响应校准";
        return $"约 {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00} 后重置";
    }
    private static string BudgetColor(double ratio) => ratio >= .95 ? "#E81123" : ratio >= .80 ? "#FFB900" : "#4C9AFF";
    partial void OnCoreBudgetRatioChanged(double value) => OnPropertyChanged(nameof(CoreBudgetColor));
    partial void OnSearchBudgetRatioChanged(double value) => OnPropertyChanged(nameof(SearchBudgetColor));
    partial void OnGraphQlBudgetRatioChanged(double value) => OnPropertyChanged(nameof(GraphQlBudgetColor));
    partial void OnContributionSourceChanged(ContributionDataSource value) => NotifyContributionText();
    private void NotifyContributionText()
    {
        OnPropertyChanged(nameof(ContributionUnit));
        OnPropertyChanged(nameof(StreakText));
        OnPropertyChanged(nameof(WeekText));
        OnPropertyChanged(nameof(TotalContributionText));
    }
}
