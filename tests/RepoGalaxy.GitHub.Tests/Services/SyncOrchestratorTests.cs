using FluentAssertions;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.GitHub.Services;
using Xunit;

namespace RepoGalaxy.GitHub.Tests.Services;

public sealed class SyncOrchestratorTests
{
    [Fact]
    public async Task Queued_requests_run_one_at_a_time_in_priority_order()
    {
        using var orchestrator = new SyncOrchestrator();
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var active = 0; var maximum = 0;
        var blocker = orchestrator.EnqueueAsync(SyncPriority.BackgroundRefresh, async token => { var value = Interlocked.Increment(ref active); maximum = Math.Max(maximum, value); await releaseFirst.Task.WaitAsync(token); order.Add("active"); Interlocked.Decrement(ref active); return 0; });
        await Task.Delay(20);
        var release = orchestrator.EnqueueAsync(SyncPriority.ReleaseCheck, async _ => { var value = Interlocked.Increment(ref active); maximum = Math.Max(maximum, value); order.Add("release"); Interlocked.Decrement(ref active); await Task.Yield(); return 1; });
        var validation = orchestrator.EnqueueAsync(SyncPriority.SessionValidation, async _ => { var value = Interlocked.Increment(ref active); maximum = Math.Max(maximum, value); order.Add("validation"); Interlocked.Decrement(ref active); await Task.Yield(); return 2; });
        releaseFirst.SetResult();

        await Task.WhenAll(blocker, release, validation);
        order.Should().Equal("active", "validation", "release");
        maximum.Should().Be(1);
    }

    [Fact]
    public async Task Cancelled_queued_request_never_executes()
    {
        using var orchestrator = new SyncOrchestrator();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = orchestrator.EnqueueAsync(SyncPriority.BackgroundRefresh, async token => { await gate.Task.WaitAsync(token); return 0; });
        using var cancellation = new CancellationTokenSource();
        var executed = false;
        var queued = orchestrator.EnqueueAsync(SyncPriority.BackgroundRefresh, _ => { executed = true; return Task.FromResult(1); }, cancellation.Token);
        cancellation.Cancel(); gate.SetResult(); await blocker;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        executed.Should().BeFalse();
    }
}
