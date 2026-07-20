using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.GitHub.Services;
using Microsoft.Extensions.Logging;

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

public sealed record ContributionCell(DateOnly Date, int Count)
{
    public double Opacity => Count switch { 0 => .08, 1 => .25, <= 3 => .48, <= 6 => .72, _ => 1 };
    public string ToolTip => $"{Date:yyyy-MM-dd} · {Count} 次提交";
}

public sealed partial class DashboardRailViewModel : ViewModelBase
{
    private readonly DashboardDataService _data;
    private readonly IExternalLinkService _links;
    private readonly ICacheService _cache;
    private readonly GitHubRequestBudget _budget;
    private readonly ILogger<DashboardRailViewModel> _logger;
    public ObservableCollection<DashboardListViewModel> TopLists { get; } = [];
    public ObservableCollection<ContributionCell> Contributions { get; } = [];
    public ObservableCollection<NewsArticle> News { get; } = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = "正在读取本地概览";
    [ObservableProperty] private int _streak;
    [ObservableProperty] private int _weekCommits;
    [ObservableProperty] private string _cacheHealth = "本地缓存就绪";
    public string StreakText => Streak == 0 ? "今天还没有提交" : $"连续贡献 {Streak} 天";
    public string WeekText => $"本周 {WeekCommits} 次提交";
    public DashboardRailViewModel(DashboardDataService data, IExternalLinkService links, ICacheService cache, GitHubRequestBudget budget, ILogger<DashboardRailViewModel> logger) { _data = data; _links = links; _cache = cache; _budget = budget; _logger = logger; }
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var snapshot = await _data.LoadAsync();
            TopLists.Clear(); TopLists.Add(new("今日增长", "24 小时关注变化", snapshot.Growth)); TopLists.Add(new("本周新秀", "创建 30 天内", snapshot.Rookies)); TopLists.Add(new("本地相关", "匹配你的工作区", snapshot.Local));
            Contributions.Clear(); foreach (var item in snapshot.Contributions) Contributions.Add(new(item.Date, item.Count));
            News.Clear(); foreach (var item in snapshot.Releases.Concat(snapshot.News).Take(7)) News.Add(item);
            Streak = snapshot.Streak; WeekCommits = snapshot.WeekCommits; OnPropertyChanged(nameof(StreakText)); OnPropertyChanged(nameof(WeekText));
            var cache = await _cache.GetStatisticsAsync(); var limit = _budget.Latest;
            CacheHealth = limit is null
                ? $"缓存 {cache.TotalBytes / 1024d / 1024d:N1} MB · 命中 {cache.HitRate:P0}"
                : $"缓存 {cache.TotalBytes / 1024d / 1024d:N1} MB · Core {limit.CoreRemaining:N0} · Search {limit.SearchRemaining:N0}";
            Status = snapshot.Contributions.Any(x => x.Count > 0) ? "本地贡献已更新" : "添加本地仓库后显示贡献";
        }
        catch (Exception ex) { _logger.LogWarning(ex, "加载工作台概览失败"); Status = "概览暂时离线，稍后会自动重试"; }
        finally { IsLoading = false; }
    }
    [RelayCommand] private void OpenNews(NewsArticle article) { if (!_links.Open(article.Url)) Status = "无法打开该资讯链接"; }
}
