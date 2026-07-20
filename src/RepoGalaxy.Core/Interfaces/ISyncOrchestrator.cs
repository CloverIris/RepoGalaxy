namespace RepoGalaxy.Core.Interfaces;

public enum SyncPriority
{
    InteractiveDetails = 0,
    SessionValidation = 1,
    LoginInitialization = 2,
    SubscriptionSync = 3,
    ReleaseCheck = 4,
    BackgroundRefresh = 5
}

public interface ISyncOrchestrator
{
    Task<T> EnqueueAsync<T>(SyncPriority priority, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
