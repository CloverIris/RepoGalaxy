using RepoGalaxy.Core.Interfaces;

namespace RepoGalaxy.GitHub.Services;

/// <summary>Single-consumer priority queue used by all GitHub REST requests.</summary>
public sealed class SyncOrchestrator : ISyncOrchestrator, IDisposable
{
    private interface IWorkItem { Task ExecuteAsync(); void Cancel(); }
    private sealed class WorkItem<T>(Func<CancellationToken, Task<T>> operation, CancellationTokenSource cancellation, TaskCompletionSource<T> completion) : IWorkItem
    {
        public async Task ExecuteAsync()
        {
            var cancellationToken = cancellation.Token;
            try
            {
                if (cancellationToken.IsCancellationRequested) { completion.TrySetCanceled(cancellationToken); return; }
                completion.TrySetResult(await operation(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { completion.TrySetCanceled(cancellationToken); }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { cancellation.Dispose(); }
        }
        public void Cancel() { cancellation.Cancel(); completion.TrySetCanceled(cancellation.Token); cancellation.Dispose(); }
    }

    private readonly object _gate = new();
    private readonly PriorityQueue<IWorkItem, (int Priority, long Sequence)> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _worker;
    private long _sequence;

    public SyncOrchestrator() => _worker = Task.Run(RunAsync);

    public Task<T> EnqueueAsync<T>(SyncPriority priority, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        lock (_gate) _queue.Enqueue(new WorkItem<T>(operation, linkedCancellation, completion), ((int)priority, Interlocked.Increment(ref _sequence)));
        _signal.Release();
        return completion.Task;
    }

    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                await _signal.WaitAsync(_lifetime.Token);
                IWorkItem? work = null;
                lock (_gate) if (_queue.Count > 0) work = _queue.Dequeue();
                if (work is not null) await work.ExecuteAsync();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        try { _worker.GetAwaiter().GetResult(); } catch { }
        lock (_gate) while (_queue.Count > 0) _queue.Dequeue().Cancel();
        _lifetime.Dispose(); _signal.Dispose();
    }
}
