using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.Services;

namespace RepoGalaxy.Desktop.Services;

public interface IGitHubQuotaService
{
    GitHubBudgetSnapshot Snapshot { get; }
    event EventHandler<GitHubBudgetSnapshot>? Changed;
    Task RestoreAndCalibrateAsync(GitHubBudgetSessionKind kind, string scopeKey, CancellationToken cancellationToken = default);
    Task<bool> RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class GitHubQuotaService : IGitHubQuotaService, IDisposable
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly GitHubApiClient _github;
    private readonly GitHubRequestBudget _budget;
    private readonly ILogger<GitHubQuotaService> _logger;
    private readonly SemaphoreSlim _persistenceGate = new(1, 1);
    private readonly HashSet<string> _calibratedScopes = new(StringComparer.Ordinal);
    private CancellationTokenSource _lifetime = new();

    public GitHubQuotaService(
        IDbContextFactory<RepoGalaxyDbContext> factory,
        GitHubApiClient github,
        GitHubRequestBudget budget,
        ILogger<GitHubQuotaService> logger)
    {
        _factory = factory;
        _github = github;
        _budget = budget;
        _logger = logger;
        _budget.Changed += OnBudgetChanged;
    }

    public GitHubBudgetSnapshot Snapshot => _budget.Snapshot;
    public event EventHandler<GitHubBudgetSnapshot>? Changed;

    public async Task RestoreAndCalibrateAsync(
        GitHubBudgetSessionKind kind,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        scopeKey = Normalize(scopeKey);
        var current = _budget.Snapshot;
        var preserveCurrent = current.SessionKind == kind
            && current.ScopeKey.Equals(scopeKey, StringComparison.OrdinalIgnoreCase);
        _budget.BeginSession(kind, scopeKey, preserveCurrent);
        await using (var db = await _factory.CreateDbContextAsync(cancellationToken))
        {
            var persisted = await db.GitHubRateBudgetSnapshots.AsNoTracking()
                .Where(x => x.ScopeKey == scopeKey)
                .ToListAsync(cancellationToken);
            foreach (var item in persisted)
            {
                var observed = item.Resource.ToLowerInvariant() switch
                {
                    "search" => _budget.Snapshot.Search,
                    "graphql" => _budget.Snapshot.GraphQl,
                    _ => _budget.Snapshot.Core
                };
                if (observed?.ObservedAt > item.ObservedAt) continue;
                _budget.Update(new GitHubRateWindow(
                    item.Resource,
                    item.Limit,
                    item.Remaining,
                    item.ResetAt,
                    item.Used,
                    item.ObservedAt,
                    item.RetryAfter));
            }
        }

        lock (_calibratedScopes)
        {
            if (!_calibratedScopes.Add(scopeKey)) return;
        }
        await RefreshAsync(cancellationToken);
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var limit = await _github.GetRateLimitAsync(cancellationToken);
            if (limit is null) return false;
            _budget.Update(limit);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "读取 GitHub API 额度失败");
            return false;
        }
    }

    private void OnBudgetChanged(object? sender, GitHubBudgetSnapshot snapshot)
    {
        Changed?.Invoke(this, snapshot);
        _ = PersistAsync(snapshot, _lifetime.Token);
    }

    private async Task PersistAsync(GitHubBudgetSnapshot snapshot, CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            await _persistenceGate.WaitAsync(cancellationToken);
            acquired = true;
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await UpsertAsync(db, snapshot.ScopeKey, snapshot.Core, cancellationToken);
            await UpsertAsync(db, snapshot.ScopeKey, snapshot.Search, cancellationToken);
            await UpsertAsync(db, snapshot.ScopeKey, snapshot.GraphQl, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            _logger.LogDebug(error, "持久化 GitHub 额度快照失败");
        }
        finally
        {
            if (acquired) _persistenceGate.Release();
        }
    }

    private static async Task UpsertAsync(
        RepoGalaxyDbContext db,
        string scopeKey,
        GitHubRateWindow? window,
        CancellationToken cancellationToken)
    {
        if (window is null) return;
        var resource = window.Resource.ToLowerInvariant() switch
        {
            "search" => "search",
            "graphql" => "graphql",
            _ => "core"
        };
        var entity = await db.GitHubRateBudgetSnapshots
            .FirstOrDefaultAsync(x => x.ScopeKey == scopeKey && x.Resource == resource, cancellationToken);
        if (entity is null)
        {
            entity = new GitHubRateBudgetSnapshotEntity { ScopeKey = scopeKey, Resource = resource };
            db.Add(entity);
        }
        entity.Limit = window.Limit;
        entity.Used = window.EffectiveUsed;
        entity.Remaining = window.Remaining;
        entity.ResetAt = window.ResetAt;
        entity.ObservedAt = window.ObservedAt ?? DateTimeOffset.UtcNow;
        entity.RetryAfter = window.RetryAfter;
    }

    private static string Normalize(string scopeKey)
        => string.IsNullOrWhiteSpace(scopeKey) ? "guest" : scopeKey.Trim().ToLowerInvariant();

    public void Dispose()
    {
        _budget.Changed -= OnBudgetChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _persistenceGate.Dispose();
    }
}

public sealed class ApiRequestTelemetryService : IApiRequestTelemetry, IDisposable
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly ILogger<ApiRequestTelemetryService> _logger;
    private readonly Channel<ApiRequestObservation> _queue =
        Channel.CreateBounded<ApiRequestObservation>(new BoundedChannelOptions(2048)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _worker;

    public ApiRequestTelemetryService(
        IDbContextFactory<RepoGalaxyDbContext> factory,
        ILogger<ApiRequestTelemetryService> logger)
    {
        _factory = factory;
        _logger = logger;
        _worker = Task.Run(() => RunAsync(_lifetime.Token));
    }

    public void Record(ApiRequestObservation observation) => _queue.Writer.TryWrite(observation);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(cancellationToken))
            {
                var hour = new DateTimeOffset(
                    item.OccurredAt.Year, item.OccurredAt.Month, item.OccurredAt.Day,
                    item.OccurredAt.Hour, 0, 0, TimeSpan.Zero);
                await using var db = await _factory.CreateDbContextAsync(cancellationToken);
                var statusClass = item.StatusCode <= 0 ? 0 : item.StatusCode / 100;
                var entity = await db.ApiRequestAggregates.FirstOrDefaultAsync(x =>
                    x.ScopeKey == item.ScopeKey
                    && x.HourBucket == hour
                    && x.Resource == item.Resource
                    && x.Operation == item.Operation
                    && x.IsNetwork == item.IsNetwork
                    && x.StatusClass == statusClass, cancellationToken);
                if (entity is null)
                {
                    entity = new ApiRequestAggregateEntity
                    {
                        ScopeKey = item.ScopeKey,
                        HourBucket = hour,
                        Resource = item.Resource,
                        Operation = item.Operation,
                        IsNetwork = item.IsNetwork,
                        StatusClass = statusClass
                    };
                    db.Add(entity);
                }
                entity.RequestCount++;
                entity.TotalDurationMilliseconds += Math.Max(0, item.DurationMilliseconds);
                await db.ApiRequestAggregates
                    .Where(x => x.HourBucket < DateTimeOffset.UtcNow.AddDays(-7))
                    .ExecuteDeleteAsync(cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "GitHub 请求统计后台任务已停止");
        }
    }

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
