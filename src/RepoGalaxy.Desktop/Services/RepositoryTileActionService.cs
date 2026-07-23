using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Desktop.Services;

public sealed class RepositoryTileActionService(
    IDbContextFactory<RepoGalaxyDbContext> factory,
    IGitHubClient github,
    IUserService users,
    IAuthenticationSessionService session) : IRepositoryTileActionService
{
    public async Task<IReadOnlyDictionary<long, RepositoryTileActionState>> LoadStatesAsync(
        IReadOnlyCollection<long> repositoryIds,
        CancellationToken cancellationToken = default)
    {
        if (repositoryIds.Count == 0) return new Dictionary<long, RepositoryTileActionState>();
        var ids = repositoryIds.Distinct().ToArray();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var feedback = await db.FeedImpressions.AsNoTracking()
            .Where(x => ids.Contains(x.RepositoryId) && (x.Action == nameof(FeedbackType.Like) || x.Action == nameof(FeedbackType.Unlike)))
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        var liked = feedback.GroupBy(x => x.RepositoryId)
            .Where(x => x.First().Action == nameof(FeedbackType.Like))
            .Select(x => x.Key)
            .ToHashSet();

        var accountId = session.Current.User?.GitHubId;
        HashSet<long> starred = [];
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            starred = (await db.UserRepositoryRelations.AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Relation == "Starred" && ids.Contains(x.RepositoryId))
                .Select(x => x.RepositoryId)
                .ToListAsync(cancellationToken)).ToHashSet();
        }

        var ignored = (await users.GetPreferencesAsync()).IgnoredTopics
            .Select(NormalizeSignal)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repositories = await db.Repositories.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.PrimaryLanguage, x.TopicsJson })
            .ToListAsync(cancellationToken);
        var suppressed = repositories.Where(x => MatchesSuppression(x.PrimaryLanguage, ReadTopics(x.TopicsJson), ignored))
            .Select(x => x.Id)
            .ToHashSet();

        return ids.ToDictionary(
            id => id,
            id => new RepositoryTileActionState(liked.Contains(id), starred.Contains(id), suppressed.Contains(id)));
    }

    public async Task<bool> SetLikedAsync(long repositoryId, bool liked, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.FeedImpressions.Add(new FeedImpressionEntity
        {
            RepositoryId = repositoryId,
            Action = liked ? nameof(FeedbackType.Like) : nameof(FeedbackType.Unlike),
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.RankingBatches.Where(x => !x.IsDirty).ExecuteUpdateAsync(
            updates => updates.SetProperty(x => x.IsDirty, true), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return liked;
    }

    public async Task<RepositoryStarResult> ToggleStarAsync(
        Repository repository,
        bool currentlyStarred,
        CancellationToken cancellationToken = default)
    {
        var accountId = session.Current.User?.GitHubId;
        if (string.IsNullOrWhiteSpace(accountId))
            return new(false, true, currentlyStarred, repository.Stars, "authentication_required");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var success = currentlyStarred
                ? await github.UnstarRepositoryAsync(repository.Owner, repository.Name)
                : await github.StarRepositoryAsync(repository.Owner, repository.Name);
            if (!success) return new(false, false, currentlyStarred, repository.Stars, "github_star_failed");

            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var relation = await db.UserRepositoryRelations.FirstOrDefaultAsync(
                x => x.AccountId == accountId && x.RepositoryId == repository.Id && x.Relation == "Starred",
                cancellationToken);
            if (currentlyStarred)
            {
                if (relation is not null) db.UserRepositoryRelations.Remove(relation);
            }
            else if (relation is null)
            {
                db.UserRepositoryRelations.Add(new UserRepositoryRelationEntity
                {
                    AccountId = accountId,
                    RepositoryId = repository.Id,
                    Relation = "Starred",
                    IsPrivate = repository.IsPrivate,
                    RelatedAt = DateTimeOffset.UtcNow
                });
            }

            var entity = await db.Repositories.FirstOrDefaultAsync(x => x.Id == repository.Id, cancellationToken);
            var stars = Math.Max(0, repository.Stars + (currentlyStarred ? -1 : 1));
            if (entity is not null) entity.Stars = stars;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, false, !currentlyStarred, stars);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return new(false, false, currentlyStarred, repository.Stars, "github_star_failed");
        }
    }

    public async Task<CategorySuppressionResult> SuppressCategoryAsync(
        Repository repository,
        IReadOnlyCollection<Repository> currentFeed,
        CancellationToken cancellationToken = default)
    {
        var topicFrequency = currentFeed
            .SelectMany(x => x.Topics.Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(NormalizeValue, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Key.Length > 0)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var signals = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(repository.PrimaryLanguage))
            signals.Add($"language:{NormalizeValue(repository.PrimaryLanguage)}");
        signals.AddRange(repository.Topics
            .Select(NormalizeValue)
            .Where(x => x.Length > 0 && topicFrequency.GetValueOrDefault(x) >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => topicFrequency[x])
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(x => $"topic:{x}"));

        var preferences = await users.GetPreferencesAsync();
        preferences.IgnoredTopics = preferences.IgnoredTopics
            .Concat(signals)
            .Select(NormalizeSignal)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        await users.SavePreferencesAsync(preferences);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.RankingBatches.Where(x => !x.IsDirty).ExecuteUpdateAsync(
            updates => updates.SetProperty(x => x.IsDirty, true), cancellationToken);
        var ignored = signals.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var affected = currentFeed.Count(x => MatchesSuppression(x.PrimaryLanguage, x.Topics, ignored));
        return new(signals, affected);
    }

    private static bool MatchesSuppression(string? language, IEnumerable<string> topics, IReadOnlySet<string> ignored)
    {
        if (!string.IsNullOrWhiteSpace(language) && ignored.Contains($"language:{NormalizeValue(language)}")) return true;
        return topics.Any(x => ignored.Contains($"topic:{NormalizeValue(x)}"));
    }

    private static string NormalizeSignal(string value)
    {
        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && parts[0] is "language" or "topic"
            ? $"{parts[0]}:{NormalizeValue(parts[1])}"
            : string.Empty;
    }

    private static string NormalizeValue(string value) =>
        string.Join('-', value.Trim().ToLowerInvariant().Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries));

    private static IReadOnlyList<string> ReadTopics(string? json)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? []; }
        catch { return []; }
    }
}
