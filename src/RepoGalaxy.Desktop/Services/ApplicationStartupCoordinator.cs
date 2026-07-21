using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Services;

namespace RepoGalaxy.Desktop.Services;

public enum StartupPhase
{
    Preparing,
    Database,
    Seeding,
    WorkspaceCleanup,
    Ready,
    Failed,
    Restoring
}

public sealed record StartupState(StartupPhase Phase, string Title, string Message, double Progress, bool CanRestore = false);

public interface IApplicationStartupCoordinator
{
    Task<DatabaseInitializationResult> InitializeAsync(IProgress<StartupState> progress, CancellationToken cancellationToken = default);
    Task<bool> RestoreLatestBackupAsync(CancellationToken cancellationToken = default);
}

public sealed class ApplicationStartupCoordinator : IApplicationStartupCoordinator
{
    private readonly DatabaseLifecycleService _lifecycle;
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly IRepositoryCloneService _cloneService;

    public ApplicationStartupCoordinator(
        DatabaseLifecycleService lifecycle,
        IDbContextFactory<RepoGalaxyDbContext> factory,
        IRepositoryCloneService cloneService)
    {
        _lifecycle = lifecycle;
        _factory = factory;
        _cloneService = cloneService;
    }

    public Task<DatabaseInitializationResult> InitializeAsync(
        IProgress<StartupState> progress,
        CancellationToken cancellationToken = default)
        => Task.Run(async () =>
        {
            progress.Report(new(StartupPhase.Preparing, "\u6b63\u5728\u51c6\u5907 RepoGalaxy", "\u6b63\u5728\u68c0\u67e5\u672c\u5730\u8fd0\u884c\u73af\u5883\u2026", .08));
            cancellationToken.ThrowIfCancellationRequested();

            progress.Report(new(StartupPhase.Database, "\u6b63\u5728\u68c0\u67e5\u672c\u5730\u6570\u636e\u5e93", "\u8fc1\u79fb\u3001\u5b8c\u6574\u6027\u68c0\u67e5\u4e0e\u6bcf\u65e5\u5907\u4efd\u5c06\u5728\u540e\u53f0\u5b8c\u6210\u3002", .25));
            var result = await _lifecycle.InitializeAsync(cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                progress.Report(new(StartupPhase.Failed, "\u672c\u5730\u6570\u636e\u9700\u8981\u4fee\u590d", result.Message, 1, CanRestore: true));
                return result;
            }

            progress.Report(new(StartupPhase.Seeding, "\u6b63\u5728\u51c6\u5907\u672c\u5730\u6570\u636e", "\u6b63\u5728\u8865\u9f50\u5e94\u7528\u6240\u9700\u7684\u672c\u5730\u9ed8\u8ba4\u8bb0\u5f55\u3002", .62));
            await using (var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
                DatabaseSeeder.Seed(db);

            progress.Report(new(StartupPhase.WorkspaceCleanup, "\u6b63\u5728\u68c0\u67e5\u672c\u5730\u5de5\u4f5c\u533a", "\u6b63\u5728\u6062\u590d\u6216\u6e05\u7406\u4e0a\u6b21\u672a\u5b8c\u6210\u7684\u514b\u9686\u4efb\u52a1\u3002", .82));
            try
            {
                await _cloneService.CleanupAbandonedAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Recoverable: cleanup is retried on the next launch.
            }

            progress.Report(new(StartupPhase.Ready, "\u51c6\u5907\u5b8c\u6210", "\u6b63\u5728\u6253\u5f00\u53d1\u73b0\u5de5\u4f5c\u53f0\u2026", 1));
            return result;
        }, cancellationToken);

    public Task<bool> RestoreLatestBackupAsync(CancellationToken cancellationToken = default)
        => Task.Run(async () => await _lifecycle.RestoreLatestBackupAsync(cancellationToken).ConfigureAwait(false), cancellationToken);
}
