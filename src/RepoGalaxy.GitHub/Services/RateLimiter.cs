namespace RepoGalaxy.GitHub.Services;

/// <summary>
/// GitHub API 请求频率限制器
/// 实现令牌桶算法控制请求频率
/// </summary>
public class RateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxRequestsPerSecond;
    private readonly Queue<DateTimeOffset> _requestTimes;
    private readonly object _lock = new();
    
    public RateLimiter(int maxRequestsPerSecond = 10)
    {
        _maxRequestsPerSecond = maxRequestsPerSecond;
        _semaphore = new SemaphoreSlim(1, 1);
        _requestTimes = new Queue<DateTimeOffset>();
    }
    
    /// <summary>
    /// 等待并获取执行许可
    /// </summary>
    public async Task WaitAsync(CancellationToken ct = default)
    {
        while (true)
        {
            TimeSpan? waitTime = null;
            
            await _semaphore.WaitAsync(ct);
            try
            {
                var now = DateTimeOffset.Now;
                var windowStart = now.AddSeconds(-1);
                
                // 清理窗口外的请求记录
                lock (_lock)
                {
                    while (_requestTimes.Count > 0 && _requestTimes.Peek() < windowStart)
                    {
                        _requestTimes.Dequeue();
                    }
                    
                    // 如果已达上限，计算等待时间
                    if (_requestTimes.Count >= _maxRequestsPerSecond)
                    {
                        var oldestRequest = _requestTimes.Peek();
                        waitTime = oldestRequest.AddSeconds(1) - now;
                    }
                    else
                    {
                        // 记录当前请求并返回
                        _requestTimes.Enqueue(DateTimeOffset.Now);
                        return;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
            
            // 需要等待时在锁外等待（在finally之后）
            if (waitTime.HasValue && waitTime.Value > TimeSpan.Zero)
            {
                await Task.Delay(waitTime.Value, ct);
            }
        }
    }
    
    /// <summary>
    /// 获取当前窗口内剩余可用请求数
    /// </summary>
    public int GetRemainingRequests()
    {
        lock (_lock)
        {
            var windowStart = DateTimeOffset.Now.AddSeconds(-1);
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < windowStart)
            {
                _requestTimes.Dequeue();
            }
            return Math.Max(0, _maxRequestsPerSecond - _requestTimes.Count);
        }
    }
}
