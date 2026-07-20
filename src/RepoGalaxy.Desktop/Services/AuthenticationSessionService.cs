using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.Services;
using RepoGalaxy.Recommendation.Services;

namespace RepoGalaxy.Desktop.Services;

public sealed class AuthenticationSessionService : IAuthenticationSessionService
{
    private readonly GitHubApiClient _github; private readonly GitHubTokenManager _tokens; private readonly IUserService _users; private readonly DiscoverySyncService _sync; private readonly IDbContextFactory<RepoGalaxyDbContext> _factory; private readonly IAuthenticationAuditService _audit; private readonly SemaphoreSlim _gate = new(1, 1);
    public AuthenticationSessionSnapshot Current { get; private set; } = new(AuthenticationSessionState.SignedOut);
    public event EventHandler<AuthenticationSessionSnapshot>? Changed;
    public AuthenticationSessionService(GitHubApiClient github, GitHubTokenManager tokens, IUserService users, DiscoverySyncService sync, IDbContextFactory<RepoGalaxyDbContext> factory, IAuthenticationAuditService audit) { _github = github; _tokens = tokens; _users = users; _sync = sync; _factory = factory; _audit = audit; }
    public async Task<AuthenticationSessionSnapshot> RestoreAsync(CancellationToken cancellationToken = default)
    {
        var token = await _tokens.GetTokenAsync(); if (string.IsNullOrWhiteSpace(token)) { await _sync.StartAsync(false, interval: await GetSyncIntervalAsync(), cancellationToken: cancellationToken); return Set(AuthenticationSessionState.SignedOut); }
        return await ValidateAndStartAsync(token, "已验证会话", false, cancellationToken);
    }
    public async Task<AuthenticationSessionSnapshot> SignInAsync(string accessToken, string method, CancellationToken cancellationToken = default) => await ValidateAndStartAsync(accessToken, method, true, cancellationToken);
    private async Task<AuthenticationSessionSnapshot> ValidateAndStartAsync(string token, string method, bool persist, CancellationToken ct)
    {
        await _gate.WaitAsync(ct); try
        {
            Set(AuthenticationSessionState.Validating); var user = await _github.ValidateTokenAsync(token, ct);
            if (user is null) { await _sync.StopAsync(); await _tokens.ClearTokenAsync(); _github.ClearAccessToken(); _audit.Record("session", "invalid", "invalid_credential"); return Set(AuthenticationSessionState.ReauthenticationRequired, error: "invalid_credential"); }
            if (persist && !await _tokens.SaveTokenAsync(token)) { await _sync.StopAsync(); _github.ClearAccessToken(); _audit.Record("session", "failed", "credential_storage"); return Set(AuthenticationSessionState.ReauthenticationRequired, error: "credential_storage"); }
            _github.SetAccessToken(token, user.GitHubId); await _users.SaveUserAsync(user); await _tokens.SaveSessionMetadataAsync(method, user.Login, "repo read:user");
            Set(AuthenticationSessionState.Initializing, user); await _sync.StartAsync(true, user.GitHubId, await GetSyncIntervalAsync(), ct); _audit.Record("session", "verified", accountId: user.GitHubId); return Set(AuthenticationSessionState.SignedIn, user);
        }
        catch (OperationCanceledException) { await _sync.StopAsync(); _github.ClearAccessToken(); return Set(AuthenticationSessionState.SignedOut, error: "cancelled"); }
        catch { await _sync.StopAsync(); _github.ClearAccessToken(); _audit.Record("session", "failed", "network_validation"); return Set(AuthenticationSessionState.ReauthenticationRequired, error: "network_validation"); }
        finally { _gate.Release(); }
    }
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken); try
        {
            var accountId = Current.User?.GitHubId ?? string.Empty;
            Set(AuthenticationSessionState.SigningOut, Current.User); await _sync.StopAsync(); await _tokens.ClearTokenAsync(); _github.ClearAccessToken();
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var privateIds = await db.UserRepositoryRelations.Where(x => x.IsPrivate).Select(x => x.RepositoryId).Distinct().ToListAsync(cancellationToken);
            if (privateIds.Count > 0) await db.Repositories.Where(x => privateIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
            await db.UserRepositoryRelations.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
            var batchIds = await db.RankingBatches.Where(x => x.AccountId == accountId).Select(x => x.Id).ToListAsync(cancellationToken);
            if (batchIds.Count > 0) await db.RankingDecisions.Where(x => batchIds.Contains(x.RankingBatchId)).ExecuteDeleteAsync(cancellationToken);
            await db.RankingBatches.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
            await db.SyncCheckpoints.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
            await db.SyncRuns.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
            await db.TileBoards.Where(x => x.ScopeKey == accountId.ToLower()).ExecuteDeleteAsync(cancellationToken);
            await db.ApiCacheEntries.Where(x => EF.Functions.Like(x.Tags, "%|private|%")).ExecuteDeleteAsync(cancellationToken);
            await db.Users.Where(x => x.GitHubId == accountId).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _audit.Record("session", "signed-out"); Set(AuthenticationSessionState.SignedOut);
        }
        finally { _gate.Release(); }
    }
    private AuthenticationSessionSnapshot Set(AuthenticationSessionState state, User? user = null, string? error = null) { Current = new(state, user, error); Changed?.Invoke(this, Current); return Current; }
    private async Task<TimeSpan> GetSyncIntervalAsync()
    {
        var minutes = (await _users.GetPreferencesAsync()).SyncIntervalMinutes;
        return minutes <= 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMinutes(minutes);
    }
}
