using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.Services;
using RepoGalaxy.GitHub.Services;

namespace RepoGalaxy.Recommendation.Services;

/// <summary>
/// 数据源类型
/// </summary>
public enum DataSourceType
{
    /// <summary>GitHub Trending</summary>
    Trending = 0,
    /// <summary>GitHub Search</summary>
    Search = 1,
    /// <summary>个性化推荐</summary>
    Personalized = 2,
    /// <summary>随机发现</summary>
    Random = 3,
    /// <summary>用户收藏</summary>
    Bookmarks = 4,
    /// <summary>浏览历史</summary>
    History = 5
}

/// <summary>
/// 数据源配置
/// </summary>
public class DataSourceConfig
{
    /// <summary>数据源类型</summary>
    public DataSourceType Type { get; set; }
    /// <summary>权重 (0-1)</summary>
    public double Weight { get; set; }
    /// <summary>最大结果数</summary>
    public int MaxResults { get; set; }
    /// <summary>刷新间隔</summary>
    public TimeSpan RefreshInterval { get; set; }
    /// <summary>上次刷新时间</summary>
    public DateTimeOffset LastRefreshed { get; set; } = DateTimeOffset.MinValue;
    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 多数据源服务
/// 整合多种数据源，提供统一的发现流数据
/// </summary>
public class DataSourceService
{
    private readonly IRepositoryService _repositoryService;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly RepositorySyncService _syncService;
    private readonly IUserService _userService;
    private readonly ILogger<DataSourceService>? _logger;
    
    // 数据源配置
    private readonly List<DataSourceConfig> _dataSourceConfigs;
    
    // 缓存
    private readonly Dictionary<DataSourceType, List<Repository>> _cachedData = new();
    private readonly Dictionary<DataSourceType, DateTimeOffset> _cacheTimestamps = new();
    
    public DataSourceService(
        IRepositoryService repositoryService,
        IRecommendationEngine recommendationEngine,
        RepositorySyncService syncService,
        IUserService userService,
        ILogger<DataSourceService>? logger = null)
    {
        _repositoryService = repositoryService;
        _recommendationEngine = recommendationEngine;
        _syncService = syncService;
        _userService = userService;
        _logger = logger;
        
        // 初始化默认数据源配置
        _dataSourceConfigs = new List<DataSourceConfig>
        {
            new()
            {
                Type = DataSourceType.Trending,
                Weight = 0.25,
                MaxResults = 30,
                RefreshInterval = TimeSpan.FromHours(1)
            },
            new()
            {
                Type = DataSourceType.Personalized,
                Weight = 0.35,
                MaxResults = 40,
                RefreshInterval = TimeSpan.FromMinutes(15)
            },
            new()
            {
                Type = DataSourceType.Search,
                Weight = 0.20,
                MaxResults = 30,
                RefreshInterval = TimeSpan.FromHours(6)
            },
            new()
            {
                Type = DataSourceType.Random,
                Weight = 0.15,
                MaxResults = 20,
                RefreshInterval = TimeSpan.FromHours(2)
            },
            new()
            {
                Type = DataSourceType.Bookmarks,
                Weight = 0.05,
                MaxResults = 10,
                RefreshInterval = TimeSpan.FromMinutes(5)
            }
        };
    }
    
    /// <summary>
    /// 获取混合发现流数据
    /// </summary>
    public async Task<IEnumerable<Repository>> GetDiscoveryFeedAsync(
        int totalCount = 50, 
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Getting discovery feed with {Count} items", totalCount);
        
        var mixedResults = new List<Repository>();
        var usedRepoIds = new HashSet<long>();
        
        foreach (var config in _dataSourceConfigs.Where(c => c.IsEnabled).OrderByDescending(c => c.Weight))
        {
            try
            {
                var count = Math.Max(1, (int)(totalCount * config.Weight));
                var repos = await GetDataSourceAsync(config.Type, count, language, cancellationToken);
                
                foreach (var repo in repos)
                {
                    if (!usedRepoIds.Contains(repo.Id))
                    {
                        mixedResults.Add(repo);
                        usedRepoIds.Add(repo.Id);
                    }
                }
                
                if (mixedResults.Count >= totalCount)
                    break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get data from source {Source}", config.Type);
            }
        }
        
        // 补充缓存数据
        if (mixedResults.Count < totalCount)
        {
            var cached = await _repositoryService.GetDiscoveryFeedAsync(1, totalCount);
            foreach (var repo in cached)
            {
                if (!usedRepoIds.Contains(repo.Id))
                {
                    mixedResults.Add(repo);
                    usedRepoIds.Add(repo.Id);
                }
            }
        }
        
        return mixedResults.Take(totalCount);
    }
    
    /// <summary>
    /// 刷新指定数据源
    /// </summary>
    public async Task<bool> RefreshDataSourceAsync(DataSourceType type, string? language = null)
    {
        _logger?.LogInformation("Refreshing data source: {Source}", type);
        
        try
        {
            var config = _dataSourceConfigs.First(c => c.Type == type);
            var repos = await FetchFromSourceAsync(type, config.MaxResults, language);
            
            _cachedData[type] = repos.ToList();
            _cacheTimestamps[type] = DateTimeOffset.Now;
            config.LastRefreshed = DateTimeOffset.Now;
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh data source {Source}", type);
            return false;
        }
    }
    
    /// <summary>
    /// 刷新所有数据源
    /// </summary>
    public async Task RefreshAllAsync(string? language = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Refreshing all data sources");
        
        foreach (var config in _dataSourceConfigs.Where(c => c.IsEnabled))
        {
            if (cancellationToken.IsCancellationRequested) break;
            await RefreshDataSourceAsync(config.Type, language);
            await Task.Delay(100, cancellationToken);
        }
    }
    
    /// <summary>
    /// 智能刷新 - 只刷新过期的数据源
    /// </summary>
    public async Task SmartRefreshAsync(string? language = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Performing smart refresh");
        
        foreach (var config in _dataSourceConfigs.Where(c => c.IsEnabled))
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            var timeSinceRefresh = DateTimeOffset.Now - config.LastRefreshed;
            if (timeSinceRefresh >= config.RefreshInterval)
            {
                await RefreshDataSourceAsync(config.Type, language);
                await Task.Delay(100, cancellationToken);
            }
        }
    }
    
    /// <summary>
    /// 获取数据源状态
    /// </summary>
    public IEnumerable<DataSourceStatus> GetDataSourceStatuses()
    {
        return _dataSourceConfigs.Select(c => new DataSourceStatus
        {
            Type = c.Type,
            IsEnabled = c.IsEnabled,
            LastRefreshed = c.LastRefreshed,
            CacheAge = _cacheTimestamps.TryGetValue(c.Type, out var ts) ? DateTimeOffset.Now - ts : TimeSpan.MaxValue,
            CachedCount = _cachedData.TryGetValue(c.Type, out var d) ? d.Count : 0
        });
    }
    
    #region 私有方法
    
    private async Task<IEnumerable<Repository>> GetDataSourceAsync(
        DataSourceType type, int count, string? language, CancellationToken cancellationToken)
    {
        if (_cachedData.TryGetValue(type, out var cached) && _cacheTimestamps.TryGetValue(type, out var ts))
        {
            var config = _dataSourceConfigs.First(c => c.Type == type);
            if (DateTimeOffset.Now - ts < config.RefreshInterval)
                return cached.Take(count);
        }
        
        await RefreshDataSourceAsync(type, language);
        return _cachedData.TryGetValue(type, out var fresh) ? fresh.Take(count) : Enumerable.Empty<Repository>();
    }
    
    private async Task<IEnumerable<Repository>> FetchFromSourceAsync(DataSourceType type, int count, string? language)
    {
        return type switch
        {
            DataSourceType.Trending => await FetchTrendingAsync(count, language),
            DataSourceType.Personalized => await FetchPersonalizedAsync(count),
            DataSourceType.Search => await FetchSearchAsync(count, language),
            DataSourceType.Random => await FetchRandomAsync(count),
            DataSourceType.Bookmarks => await FetchBookmarksAsync(count),
            DataSourceType.History => await FetchHistoryAsync(count),
            _ => Enumerable.Empty<Repository>()
        };
    }
    
    private async Task<IEnumerable<Repository>> FetchTrendingAsync(int count, string? language)
    {
        try
        {
            await _syncService.SyncTrendingAsync(language, "daily");
            var trending = await _repositoryService.GetTrendingAsync(language, "daily");
            return trending.Take(count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch trending");
            return await _repositoryService.GetTrendingAsync(language, "daily");
        }
    }
    
    private async Task<IEnumerable<Repository>> FetchPersonalizedAsync(int count)
    {
        return await _recommendationEngine.GetRecommendationsAsync(count);
    }
    
    private async Task<IEnumerable<Repository>> FetchSearchAsync(int count, string? language)
    {
        try
        {
            var preferences = await _userService.GetPreferencesAsync();
            var results = new List<Repository>();
            
            var searchLanguages = !string.IsNullOrEmpty(language) 
                ? new[] { language } 
                : preferences.InterestedLanguages.Take(3).ToArray();
            
            foreach (var lang in searchLanguages)
            {
                if (results.Count >= count) break;
                await _syncService.SyncSearchResultsAsync($"language:{lang} stars:>100", lang, count);
                var searchResults = await _repositoryService.SearchAsync(lang);
                results.AddRange(searchResults);
            }
            
            return results.Distinct().Take(count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch search results");
            return Enumerable.Empty<Repository>();
        }
    }
    
    private async Task<IEnumerable<Repository>> FetchRandomAsync(int count)
    {
        var cached = await _repositoryService.GetCachedAsync(TimeSpan.FromDays(30));
        var random = new Random();
        return cached.Where(r => !r.IsIgnored && r.DiscoveryScore > 0.5)
                     .OrderBy(_ => random.Next())
                     .Take(count);
    }
    
    private async Task<IEnumerable<Repository>> FetchBookmarksAsync(int count)
    {
        var bookmarks = await _repositoryService.GetBookmarksAsync();
        return bookmarks.Take(count);
    }
    
    private async Task<IEnumerable<Repository>> FetchHistoryAsync(int count)
    {
        var history = await _repositoryService.GetCachedAsync(TimeSpan.FromDays(7));
        return history.OrderByDescending(r => r.LastViewedAt).Take(count);
    }
    
    #endregion
}

/// <summary>
/// 数据源状态
/// </summary>
public class DataSourceStatus
{
    public DataSourceType Type { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset LastRefreshed { get; set; }
    public TimeSpan CacheAge { get; set; }
    public int CachedCount { get; set; }
    
    public string DisplayName => Type switch
    {
        DataSourceType.Trending => "热门趋势",
        DataSourceType.Search => "搜索发现",
        DataSourceType.Personalized => "个性化推荐",
        DataSourceType.Random => "随机发现",
        DataSourceType.Bookmarks => "我的收藏",
        DataSourceType.History => "浏览历史",
        _ => "未知"
    };
    
    public bool IsStale => CacheAge > TimeSpan.FromHours(1);
}
