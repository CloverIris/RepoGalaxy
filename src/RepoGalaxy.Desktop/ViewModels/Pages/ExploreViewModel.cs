using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Desktop.Models;

namespace RepoGalaxy.Desktop.ViewModels;

/// <summary>
/// 探索页面 ViewModel - 气泡云可视化
/// 支持多种数据源：推荐、Trending、搜索、收藏
/// </summary>
public partial class ExploreViewModel : ViewModelBase
{
    private readonly ILogger<ExploreViewModel> _logger;
    private readonly RepositoryService _repoService;
    private readonly IRecommendationEngine _recommendationEngine;

    // 气泡云数据
    [ObservableProperty] private ObservableCollection<BubbleItem> _bubbleItems = new();
    [ObservableProperty] private BubbleItem? _selectedBubble;
    
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _showEmptyState;

    // 筛选选项
    [ObservableProperty] private string _selectedLanguage = "全部";
    [ObservableProperty] private string _selectedSort = "推荐";
    [ObservableProperty] private string _selectedDataSource = "智能推荐";

    // 数据源选项
    public List<string> DataSourceOptions { get; } = new()
    {
        "智能推荐",
        "热门趋势", 
        "我的收藏",
        "最近浏览"
    };

    public List<string> LanguageOptions { get; } = new()
    {
        "全部", "C#", "Python", "JavaScript", "TypeScript", "Go", "Rust", "Java"
    };

    public List<string> SortOptions { get; } = new()
    {
        "推荐", "星标数", "最近更新", "Fork数"
    };

    public ExploreViewModel(
        ILogger<ExploreViewModel> logger,
        RepositoryService repoService,
        IRecommendationEngine recommendationEngine)
    {
        _logger = logger;
        _repoService = repoService;
        _recommendationEngine = recommendationEngine;

        // 初始化时加载数据
        _ = RefreshAsync();
    }

    /// <summary>
    /// 刷新数据 - 根据当前选择的数据源加载
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            ShowEmptyState = false;

            IEnumerable<Repository> repos;
            
            // 根据数据源类型加载数据
            switch (SelectedDataSource)
            {
                case "智能推荐":
                    StatusMessage = "正在计算个性化推荐...";
                    repos = await _recommendationEngine.GetRecommendationsAsync(50);
                    break;
                    
                case "热门趋势":
                    StatusMessage = "正在获取热门趋势...";
                    var language = SelectedLanguage == "全部" ? null : SelectedLanguage;
                    repos = await _repoService.GetTrendingAsync(language, "daily");
                    break;
                    
                case "我的收藏":
                    StatusMessage = "正在加载收藏...";
                    repos = await _repoService.GetBookmarksAsync();
                    break;
                    
                case "最近浏览":
                    StatusMessage = "正在加载浏览历史...";
                    repos = await _repoService.GetCachedAsync(TimeSpan.FromDays(7));
                    break;
                    
                default:
                    repos = await _repoService.GetDiscoveryFeedAsync(1, 50);
                    break;
            }

            // 应用筛选和排序
            var filtered = ApplyFilters(repos);
            
            // 转换为 BubbleItem
            var bubbles = filtered.Select(BubbleItem.FromRepository).ToList();
            
            BubbleItems.Clear();
            foreach (var bubble in bubbles)
            {
                BubbleItems.Add(bubble);
            }

            if (BubbleItems.Count == 0)
            {
                ShowEmptyState = true;
                StatusMessage = SelectedDataSource switch
                {
                    "智能推荐" => "暂无推荐数据，请浏览更多仓库以获取个性化推荐",
                    "热门趋势" => "暂无趋势数据，请检查网络连接",
                    "我的收藏" => "暂无收藏，点击星标收藏感兴趣的仓库",
                    "最近浏览" => "暂无浏览记录",
                    _ => "暂无数据"
                };
            }
            else
            {
                StatusMessage = $"{SelectedDataSource}: 发现 {BubbleItems.Count} 个代码宝藏";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载探索数据失败");
            StatusMessage = $"加载失败: {ex.Message}";
            ShowEmptyState = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 搜索仓库
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await RefreshAsync();
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "搜索中...";

            // 先搜索本地缓存
            var localResults = await _repoService.SearchAsync(SearchQuery);
            
            // 如果本地结果太少，尝试从 GitHub 搜索
            if (localResults.Count() < 5)
            {
                StatusMessage = "本地数据不足，正在搜索 GitHub...";
                // 触发后台同步（通过 RepositorySyncService）
                // 这里简化处理，仅使用本地结果
            }
            
            var bubbles = localResults.Select(BubbleItem.FromRepository).ToList();
            
            BubbleItems.Clear();
            foreach (var bubble in bubbles)
            {
                BubbleItems.Add(bubble);
            }

            StatusMessage = $"搜索 '{SearchQuery}': 找到 {BubbleItems.Count} 个结果";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索失败");
            StatusMessage = $"搜索失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 获取相似仓库（用于聚类推荐）
    /// </summary>
    [RelayCommand]
    private async Task FindSimilarAsync(long repositoryId)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "正在寻找相似项目...";

            var similar = await _recommendationEngine.GetSimilarAsync(repositoryId, 20);
            
            var bubbles = similar.Select(BubbleItem.FromRepository).ToList();
            
            BubbleItems.Clear();
            foreach (var bubble in bubbles)
            {
                BubbleItems.Add(bubble);
            }

            StatusMessage = $"发现 {bubbles.Count} 个相似项目";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查找相似项目失败");
            StatusMessage = "查找失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 切换收藏状态
    /// </summary>
    [RelayCommand]
    private async Task ToggleBookmarkAsync(long repositoryId)
    {
        try
        {
            await _repoService.ToggleBookmarkAsync(repositoryId);
            
            // 更新本地气泡状态
            var bubble = BubbleItems.FirstOrDefault(b => b.Id == repositoryId);
            if (bubble != null)
            {
                bubble.IsBookmarked = !bubble.IsBookmarked;
            }
            
            // 记录用户反馈到推荐引擎
            await _recommendationEngine.RecordFeedbackAsync(
                repositoryId, 
                bubble?.IsBookmarked == true ? FeedbackType.Bookmark : FeedbackType.Dismiss);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换收藏状态失败");
        }
    }

    [ObservableProperty] private string _searchQuery = string.Empty;

    // 属性变更回调
    partial void OnSelectedLanguageChanged(string value) => _ = RefreshAsync();
    partial void OnSelectedSortChanged(string value) => _ = RefreshAsync();
    partial void OnSelectedDataSourceChanged(string value) => _ = RefreshAsync();

    /// <summary>
    /// 应用筛选和排序
    /// </summary>
    private IEnumerable<Repository> ApplyFilters(IEnumerable<Repository> repos)
    {
        // 语言筛选（仅在非 Trending 模式下应用，因为 Trending 已按语言过滤）
        if (SelectedLanguage != "全部" && SelectedDataSource != "热门趋势")
        {
            repos = repos.Where(r => 
                r.PrimaryLanguage?.Equals(SelectedLanguage, StringComparison.OrdinalIgnoreCase) == true);
        }

        // 排序
        repos = SelectedSort switch
        {
            "星标数" => repos.OrderByDescending(r => r.Stars),
            "最近更新" => repos.OrderByDescending(r => r.UpdatedAt),
            "Fork数" => repos.OrderByDescending(r => r.Forks),
            _ => repos.OrderByDescending(r => r.DiscoveryScore) // 推荐
        };

        return repos;
    }
}

/// <summary>
/// 仓库数据包装 ViewModel - 用于卡片列表视图
/// </summary>
public class RepositoryViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private readonly Repository _repo;

    public RepositoryViewModel(Repository repo)
    {
        _repo = repo;
    }

    public long Id => _repo.Id;
    public string Name => _repo.Name;
    public string Owner => _repo.Owner;
    public string FullName => _repo.FullName;
    public string Description => _repo.Description ?? "暂无描述";
    public long Stars => _repo.Stars;
    public long Forks => _repo.Forks;
    public string? PrimaryLanguage => _repo.PrimaryLanguage ?? "Unknown";
    public DateTimeOffset LastUpdated => _repo.UpdatedAt;
    public bool IsBookmarked => _repo.IsBookmarked;
    public double Score => _repo.DiscoveryScore;

    public string StarsFormatted => FormatNumber(Stars);
    public string ForksFormatted => FormatNumber(Forks);

    private static string FormatNumber(long num)
    {
        return num switch
        {
            >= 1000000 => $"{num / 1000000.0:F1}M",
            >= 1000 => $"{num / 1000.0:F1}k",
            _ => num.ToString()
        };
    }
}
