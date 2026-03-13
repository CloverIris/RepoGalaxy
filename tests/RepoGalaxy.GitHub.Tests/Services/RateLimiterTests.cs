using FluentAssertions;
using RepoGalaxy.GitHub.Services;
using Xunit;

namespace RepoGalaxy.GitHub.Tests.Services;

public class RateLimiterTests
{
    [Fact]
    public async Task WaitAsync_UnderLimit_AllowsImmediately()
    {
        // Arrange
        var limiter = new RateLimiter(maxRequestsPerSecond: 10);

        // Act - 发起5个请求（低于限制）
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => limiter.WaitAsync())
            .ToList();

        // Assert - 应该立即完成
        await Task.WhenAll(tasks);
        tasks.All(t => t.IsCompletedSuccessfully).Should().BeTrue();
    }

    [Fact]
    public async Task GetRemainingRequests_AfterRequests_ReturnsCorrectCount()
    {
        // Arrange
        var limiter = new RateLimiter(maxRequestsPerSecond: 5);
        
        // Act - 发起3个请求
        for (int i = 0; i < 3; i++)
        {
            await limiter.WaitAsync();
        }

        // Assert
        limiter.GetRemainingRequests().Should().Be(2);
    }

    [Fact]
    public async Task GetRemainingRequests_InitialState_ReturnsMax()
    {
        // Arrange
        var limiter = new RateLimiter(maxRequestsPerSecond: 10);

        // Assert
        limiter.GetRemainingRequests().Should().Be(10);
    }

    [Fact]
    public async Task WaitAsync_ExceedLimit_WaitsForWindow()
    {
        // Arrange - 每秒最多2个请求
        var limiter = new RateLimiter(maxRequestsPerSecond: 2);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - 发起3个请求（超过限制）
        await limiter.WaitAsync();
        await limiter.WaitAsync();
        await limiter.WaitAsync(); // 这个应该等待
        stopwatch.Stop();

        // Assert - 第三个请求应该等待至少1秒
        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
    }

    [Fact]
    public async Task WaitAsync_AfterWindowReset_AllowsNewRequests()
    {
        // Arrange - 每秒最多1个请求
        var limiter = new RateLimiter(maxRequestsPerSecond: 1);
        await limiter.WaitAsync(); // 使用1个配额

        // Act - 等待窗口重置
        await Task.Delay(1100);
        
        // Assert - 应该又有配额
        limiter.GetRemainingRequests().Should().Be(1);
        
        // 新的请求应该可以立即执行
        var task = limiter.WaitAsync();
        await task;
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task GetRemainingRequests_ConcurrentAccess_DoesNotThrow()
    {
        // Arrange
        var limiter = new RateLimiter(maxRequestsPerSecond: 100);

        // Act & Assert - 并发访问不应抛出异常
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => limiter.GetRemainingRequests()))
            .ToArray();

        await Task.WhenAll(tasks);
        tasks.All(t => t.IsCompletedSuccessfully).Should().BeTrue();
    }
}
