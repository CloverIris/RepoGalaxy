using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.GitHub.Services;
using RepoGalaxy.Recommendation.Services;
using System.Collections.Concurrent;

namespace RepoGalaxy.Recommendation.Services;

/// <summary>
/// 后台同步服务
/// 管理定期数据同步和后台任务
/// </summary>
public class BackgroundSyncService : IDisposable
{
    private readonly DataSourceService _dataSourceService;
    private readonly RepositorySyncService _repositorySyncService;
    private readonly IUserService _userService;
    private readonly ILogger<BackgroundSyncService>? _logger;
    
    private readonly ConcurrentDictionary<string, BackgroundTask> _runningTasks = new();
    private readonly CancellationTokenSource _serviceCts = new();
    private readonly Timer? _periodicTimer;
    
    // 同步状态
    public bool IsRunning { get; private set; }
    public DateTimeOffset LastSyncTime { get; private set; } = DateTimeOffset.MinValue;
    public TimeSpan LastSyncDuration { get; private set; }
    
    // 统计
    public int TotalSyncCount { get; private set; }
    public int SuccessfulSyncCount { get; private set; }
    public int FailedSyncCount { get; private set; }
    
    // 事件
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
    public event EventHandler<SyncProgressEventArgs>? SyncProgress;
    
    public BackgroundSyncService(
        DataSourceService dataSourceService,
        RepositorySyncService repositorySyncService,
        IUserService userService,
        ILogger<BackgroundSyncService>? logger = null)
    {
        _dataSourceService = dataSourceService;
        _repositorySyncService = repositorySyncService;
        _userService = userService;
        _logger = logger;
        
        // 创建周期性定时器（每30分钟检查一次）
        _periodicTimer = new Timer(OnPeriodicTimerCallback, null, 
            TimeSpan.FromMinutes(5), // 首次延迟5分钟
            TimeSpan.FromMinutes(30)); // 之后每30分钟
    }
    
    /// <summary>
    /// 启动后台同步服务
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;
        
        _logger?.LogInformation("Background sync service started");
        IsRunning = true;
        
        // 启动时执行一次智能刷新
        _ = PerformStartupSyncAsync();
    }
    
    /// <summary>
    /// 停止后台同步服务
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;
        
        _logger?.LogInformation("Background sync service stopping...");
        _serviceCts.Cancel();
        IsRunning = false;
    }
    
    /// <summary>
    /// 执行完整同步
    /// </summary>
    public async Task<SyncResult> PerformFullSyncAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.Now;
        var taskId = Guid.NewGuid().ToString("N")[..8];
        
        _logger?.LogInformation("Starting full sync [Task:{TaskId}]", taskId);
        OnSyncProgress(taskId, "开始完整同步", 0);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_serviceCts.Token, cancellationToken);
        var task = new BackgroundTask
        {
            Id = taskId,
            Type = SyncTaskType.FullSync,
            StartTime = startTime,
            CancellationTokenSource = cts
        };
        
        _runningTasks[taskId] = task;
        
        try
        {
            var result = new SyncResult { TaskId = taskId };
            
            // 1. 检查认证状态
            OnSyncProgress(taskId, "检查认证状态", 10);
            if (!await _userService.IsAuthenticatedAsync())
            {
                _logger?.LogWarning("Sync skipped: not authenticated");
                result.Status = SyncStatus.Skipped;
                result.Message = "未登录，跳过同步";
                return result;
            }
            
            // 2. 同步用户仓库
            OnSyncProgress(taskId, "同步用户仓库", 20);
            try
            {
                var userReposResult = await _repositorySyncService.SyncUserRepositoriesAsync();
                result.UserReposAdded = userReposResult.Added;
                result.UserReposUpdated = userReposResult.Updated;
                _logger?.LogInformation("Synced user repos: +{Added} ~{Updated}", 
                    userReposResult.Added, userReposResult.Updated);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to sync user repositories");
                result.Errors.Add($"用户仓库同步失败: {ex.Message}");
            }
            
            // 3. 刷新多数据源
            OnSyncProgress(taskId, "刷新数据源", 50);
            try
            {
                await _dataSourceService.RefreshAllAsync(null, cts.Token);
                _logger?.LogInformation("Data sources refreshed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to refresh data sources");
                result.Errors.Add($"数据源刷新失败: {ex.Message}");
            }
            
            // 4. 更新用户偏好（基于历史）
            OnSyncProgress(taskId, "更新用户偏好", 80);
            try
            {
                await _userService.UpdateInterestedTopicsFromHistoryAsync();
                _logger?.LogInformation("User preferences updated");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to update user preferences from history");
            }
            
            // 5. 清理过期缓存
            OnSyncProgress(taskId, "清理缓存", 90);
            try
            {
                var preferences = await _userService.GetPreferencesAsync();
                if (preferences.AutoCleanCache)
                {
                    await _repositorySyncService.ClearOldCacheAsync(TimeSpan.FromDays(7));
                    _logger?.LogInformation("Old cache cleared");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clear old cache");
            }
            
            // 完成
            result.EndTime = DateTimeOffset.Now;
            result.Status = result.Errors.Any() ? SyncStatus.PartialSuccess : SyncStatus.Success;
            result.Message = result.Status == SyncStatus.Success 
                ? "同步完成" 
                : $"部分成功，有 {result.Errors.Count} 个错误";
            
            LastSyncTime = result.EndTime;
            LastSyncDuration = result.EndTime - result.StartTime;
            TotalSyncCount++;
            if (result.Status == SyncStatus.Success) SuccessfulSyncCount++;
            else if (result.Status == SyncStatus.Failed) FailedSyncCount++;
            
            OnSyncProgress(taskId, "同步完成", 100);
            OnSyncCompleted(result);
            
            return result;
        }
        finally
        {
            _runningTasks.TryRemove(taskId, out _);
        }
    }
    
    /// <summary>
    /// 执行快速同步（仅关键数据）
    /// </summary>
    public async Task<SyncResult> PerformQuickSyncAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.Now;
        var taskId = Guid.NewGuid().ToString("N")[..8];
        
        _logger?.LogInformation("Starting quick sync [Task:{TaskId}]", taskId);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_serviceCts.Token, cancellationToken);
        
        try
        {
            var result = new SyncResult { TaskId = taskId, StartTime = startTime };
            
            // 只执行智能刷新
            await _dataSourceService.SmartRefreshAsync(null, cts.Token);
            
            result.EndTime = DateTimeOffset.Now;
            result.Status = SyncStatus.Success;
            result.Message = "快速同步完成";
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Quick sync failed");
            return new SyncResult
            {
                TaskId = taskId,
                StartTime = startTime,
                EndTime = DateTimeOffset.Now,
                Status = SyncStatus.Failed,
                Message = $"同步失败: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// 取消正在运行的任务
    /// </summary>
    public void CancelTask(string taskId)
    {
        if (_runningTasks.TryGetValue(taskId, out var task))
        {
            task.CancellationTokenSource?.Cancel();
            _logger?.LogInformation("Task {TaskId} cancellation requested", taskId);
        }
    }
    
    /// <summary>
    /// 获取正在运行的任务
    /// </summary>
    public IEnumerable<BackgroundTask> GetRunningTasks()
    {
        return _runningTasks.Values.ToList();
    }
    
    /// <summary>
    /// 获取同步状态摘要
    /// </summary>
    public SyncStatusSummary GetStatusSummary()
    {
        return new SyncStatusSummary
        {
            IsRunning = IsRunning,
            LastSyncTime = LastSyncTime,
            LastSyncDuration = LastSyncDuration,
            TotalSyncCount = TotalSyncCount,
            SuccessfulSyncCount = SuccessfulSyncCount,
            FailedSyncCount = FailedSyncCount,
            RunningTaskCount = _runningTasks.Count,
            DataSourceStatuses = _dataSourceService.GetDataSourceStatuses().ToList()
        };
    }
    
    #region 私有方法
    
    private async void OnPeriodicTimerCallback(object? state)
    {
        if (!IsRunning) return;
        
        try
        {
            _logger?.LogDebug("Periodic sync check");
            
            // 检查是否需要同步（距离上次超过2小时）
            if (DateTimeOffset.Now - LastSyncTime > TimeSpan.FromHours(2))
            {
                _logger?.LogInformation("Starting periodic sync");
                await PerformQuickSyncAsync(_serviceCts.Token);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Periodic sync failed");
        }
    }
    
    private async Task PerformStartupSyncAsync()
    {
        try
        {
            // 延迟几秒等应用完全启动
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            // 检查是否首次启动（从未同步过）
            if (LastSyncTime == DateTimeOffset.MinValue)
            {
                _logger?.LogInformation("First launch detected, performing initial sync");
                await PerformFullSyncAsync(_serviceCts.Token);
            }
            else
            {
                // 否则执行快速同步
                await PerformQuickSyncAsync(_serviceCts.Token);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Startup sync failed");
        }
    }
    
    private void OnSyncProgress(string taskId, string message, int progressPercent)
    {
        SyncProgress?.Invoke(this, new SyncProgressEventArgs
        {
            TaskId = taskId,
            Message = message,
            ProgressPercent = progressPercent,
            Timestamp = DateTimeOffset.Now
        });
    }
    
    private void OnSyncCompleted(SyncResult result)
    {
        SyncCompleted?.Invoke(this, new SyncCompletedEventArgs
        {
            Result = result,
            Timestamp = DateTimeOffset.Now
        });
    }
    
    public void Dispose()
    {
        Stop();
        _serviceCts.Cancel();
        _serviceCts.Dispose();
        _periodicTimer?.Dispose();
    }
    
    #endregion
}

/// <summary>
/// 同步任务类型
/// </summary>
public enum SyncTaskType
{
    FullSync,
    QuickSync,
    TrendingSync,
    UserRepoSync
}

/// <summary>
/// 同步状态
/// </summary>
public enum SyncStatus
{
    Pending,
    Running,
    Success,
    PartialSuccess,
    Failed,
    Skipped,
    Cancelled
}

/// <summary>
/// 后台任务
/// </summary>
public class BackgroundTask
{
    public string Id { get; set; } = string.Empty;
    public SyncTaskType Type { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
}

/// <summary>
/// 同步结果
/// </summary>
public class SyncResult
{
    public string TaskId { get; set; } = string.Empty;
    public SyncStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    
    // 统计
    public int UserReposAdded { get; set; }
    public int UserReposUpdated { get; set; }
    public int TrendingAdded { get; set; }
    public int SearchAdded { get; set; }
    
    // 错误
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 同步进度事件参数
/// </summary>
public class SyncProgressEventArgs : EventArgs
{
    public string TaskId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// 同步完成事件参数
/// </summary>
public class SyncCompletedEventArgs : EventArgs
{
    public SyncResult Result { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// 同步状态摘要
/// </summary>
public class SyncStatusSummary
{
    public bool IsRunning { get; set; }
    public DateTimeOffset LastSyncTime { get; set; }
    public TimeSpan LastSyncDuration { get; set; }
    public int TotalSyncCount { get; set; }
    public int SuccessfulSyncCount { get; set; }
    public int FailedSyncCount { get; set; }
    public int RunningTaskCount { get; set; }
    public List<DataSourceStatus> DataSourceStatuses { get; set; } = new();
    
    public bool IsStale => DateTimeOffset.Now - LastSyncTime > TimeSpan.FromHours(2);
    public double SuccessRate => TotalSyncCount > 0 ? (double)SuccessfulSyncCount / TotalSyncCount : 0;
}
