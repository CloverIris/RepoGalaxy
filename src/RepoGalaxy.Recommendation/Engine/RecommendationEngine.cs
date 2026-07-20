using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Recommendation.Engine;

public sealed class RecommendationEngine : IRecommendationEngine
{
    private readonly IRepositoryService _repositories;
    private readonly IUserService _users;
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly IRankingPipeline _pipeline;
    public RecommendationEngine(IRepositoryService repositories, IUserService users, IDbContextFactory<RepoGalaxyDbContext> factory, IRankingPipeline pipeline) { _repositories = repositories; _users = users; _factory = factory; _pipeline = pipeline; }

    public async Task<IEnumerable<Repository>> GetRecommendationsAsync(int count = 20)
        => (await GetRankedRecommendationsAsync(count)).Select(x => x.Result.Repository).ToList();

    public async Task<IReadOnlyList<RankedRecommendation>> GetRankedRecommendationsAsync(int count = 60)
    {
        var preferences = await _users.GetPreferencesAsync();
        var candidates = (await _repositories.GetCachedAsync(TimeSpan.FromDays(7))).ToList();
        if (candidates.Count == 0) return [];
        var feedback = await LoadFeedbackAsync();
        var account = (await _users.GetCurrentUserAsync())?.GitHubId ?? "guest";
        var batch = $"ranking-v1:{account}:{DateTime.UtcNow:yyyyMMdd}";
        var metrics = await LoadMetricVelocitiesAsync(candidates.Select(x => x.Id));
        var rules = await LoadRuleMatchesAsync(candidates);
        var localLanguages = DetectLocalLanguages(await LoadLocalPathsAsync());
        var context = new RankingContext(new HashSet<string>(preferences.InterestedLanguages, StringComparer.OrdinalIgnoreCase), new HashSet<string>(preferences.InterestedTopics, StringComparer.OrdinalIgnoreCase), localLanguages, feedback, batch, metrics, rules);
        var coarse = _pipeline.CoarseRank(new CandidateSet(candidates, "ForYou", DateTimeOffset.UtcNow), context);
        var ranked = _pipeline.FineRank(coarse, context, count)
            .Select(x => { x.Repository.DiscoveryScore = x.FineScore; return new RankedRecommendation(x, _pipeline.Explain(x), batch); })
            .ToList();
        await PersistBatchAsync(account, batch, ranked);
        return ranked;
    }
    public async Task<IEnumerable<Repository>> GetSimilarAsync(long repositoryId, int count = 10) { var source = await _repositories.GetByIdAsync(repositoryId); if (source is null) return []; var all = await _repositories.GetCachedAsync(TimeSpan.FromDays(30)); return all.Where(x => x.Id != source.Id).Select(x => (Repo: x, Score: Similarity(source, x))).OrderByDescending(x => x.Score).Take(count).Select(x => x.Repo).ToList(); }
    public async Task<IEnumerable<Repository>> GetRelatedRecommendationsAsync(IEnumerable<long> seedIds, int count = 15) { var ids = seedIds.ToHashSet(); var seeds = new List<Repository>(); foreach (var id in ids) if (await _repositories.GetByIdAsync(id) is { } r) seeds.Add(r); if (seeds.Count == 0) return []; var all = await _repositories.GetCachedAsync(TimeSpan.FromDays(30)); return all.Where(x => !ids.Contains(x.Id)).Select(x => (Repo: x, Score: seeds.Max(s => Similarity(s, x)))).OrderByDescending(x => x.Score).Take(count).Select(x => x.Repo).ToList(); }
    public Task UpdateUserProfileAsync() => Task.CompletedTask;
    public async Task RecordFeedbackAsync(long repositoryId, FeedbackType type) { await using var db = await _factory.CreateDbContextAsync(); db.FeedImpressions.Add(new FeedImpressionEntity { RepositoryId = repositoryId, Action = type.ToString(), OccurredAt = DateTimeOffset.UtcNow }); var batches = await db.RankingBatches.Where(x => !x.IsDirty).ToListAsync(); foreach (var b in batches) b.IsDirty = true; await db.SaveChangesAsync(); }
    private async Task<IReadOnlyDictionary<long, FeedbackType>> LoadFeedbackAsync() { await using var db = await _factory.CreateDbContextAsync(); var rows = await db.FeedImpressions.AsNoTracking().OrderByDescending(x => x.OccurredAt).Take(500).ToListAsync(); return rows.GroupBy(x => x.RepositoryId).ToDictionary(x => x.Key, x => Enum.TryParse<FeedbackType>(x.First().Action, out var value) ? value : FeedbackType.View); }
    private async Task<IReadOnlyDictionary<long, double>> LoadMetricVelocitiesAsync(IEnumerable<long> repositoryIds)
    {
        var ids = repositoryIds.ToList();
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.RepositoryMetricSnapshots.AsNoTracking().Where(x => ids.Contains(x.RepositoryId)).ToListAsync();
        var deltas = rows.GroupBy(x => x.RepositoryId).ToDictionary(x => x.Key, x =>
        {
            var ordered = x.OrderByDescending(y => y.SnapshotDate).Take(2).ToList();
            return ordered.Count < 2 ? 0 : Math.Max(0, ordered[0].Stars - ordered[1].Stars);
        });
        var max = Math.Max(1, deltas.Values.DefaultIfEmpty().Max());
        return deltas.ToDictionary(x => x.Key, x => Math.Clamp(Math.Log(1 + x.Value) / Math.Log(1 + max), 0, 1));
    }
    private async Task<IReadOnlyDictionary<long, double>> LoadRuleMatchesAsync(IReadOnlyList<Repository> repositories)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var subscriptions = await db.DiscoverySubscriptions.AsNoTracking().Where(x => x.IsEnabled).ToListAsync();
        var result = new Dictionary<long, double>();
        foreach (var repository in repositories)
        {
            var best = 0d;
            foreach (var subscription in subscriptions)
            {
                var topics = ReadStrings(subscription.TopicsJson);
                var languages = ReadStrings(subscription.LanguagesJson);
                var keywords = ReadStrings(subscription.KeywordsJson);
                var signals = 0;
                if (topics.Any(x => repository.Topics.Contains(x, StringComparer.OrdinalIgnoreCase))) signals++;
                if (languages.Contains(repository.PrimaryLanguage, StringComparer.OrdinalIgnoreCase)) signals++;
                if (keywords.Any(x => repository.Name.Contains(x, StringComparison.OrdinalIgnoreCase) || repository.Description.Contains(x, StringComparison.OrdinalIgnoreCase))) signals++;
                best = Math.Max(best, signals / 3d);
            }
            result[repository.Id] = best;
        }
        return result;
    }
    private async Task<IReadOnlyList<string>> LoadLocalPathsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.LocalRepositories.AsNoTracking().Where(x => x.IsTracked).Select(x => x.LocalPath).ToListAsync();
    }
    private async Task PersistBatchAsync(string account, string batchId, IReadOnlyList<RankedRecommendation> ranked)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var batch = await db.RankingBatches.FirstOrDefaultAsync(x => x.BatchId == batchId);
        if (batch is null)
        {
            batch = new RankingBatchEntity { BatchId = batchId, AccountId = account, Source = "ForYou", AlgorithmVersion = "heuristic-v1", CreatedAt = DateTimeOffset.UtcNow };
            db.RankingBatches.Add(batch);
            await db.SaveChangesAsync();
        }
        await db.RankingDecisions.Where(x => x.RankingBatchId == batch.Id).ExecuteDeleteAsync();
        foreach (var item in ranked)
            db.RankingDecisions.Add(new RankingDecisionEntity { RankingBatchId = batch.Id, RepositoryId = item.Result.Repository.Id, CoarseScore = item.Result.CoarseScore, FineScore = item.Result.FineScore, Position = item.Result.Position, IsExploration = item.Result.IsExploration, FeaturesJson = JsonSerializer.Serialize(item.Result.Features), Explanation = JsonSerializer.Serialize(item.Explanation) });
        batch.IsDirty = false;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    private static HashSet<string> DetectLocalLanguages(IEnumerable<string> paths)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Where(Directory.Exists))
        {
            try
            {
                if (Directory.EnumerateFiles(path, "*.csproj", SearchOption.TopDirectoryOnly).Any()) result.Add("C#");
                if (File.Exists(Path.Combine(path, "package.json"))) result.Add("TypeScript");
                if (File.Exists(Path.Combine(path, "Cargo.toml"))) result.Add("Rust");
                if (File.Exists(Path.Combine(path, "go.mod"))) result.Add("Go");
                if (File.Exists(Path.Combine(path, "pyproject.toml")) || File.Exists(Path.Combine(path, "requirements.txt"))) result.Add("Python");
            }
            catch { }
        }
        return result;
    }
    private static List<string> ReadStrings(string json) { try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; } catch { return []; } }
    private static double Similarity(Repository a, Repository b) { var language = a.PrimaryLanguage.Equals(b.PrimaryLanguage, StringComparison.OrdinalIgnoreCase) ? .5 : 0; var topics = a.Topics.Union(b.Topics, StringComparer.OrdinalIgnoreCase).Count(); var common = a.Topics.Intersect(b.Topics, StringComparer.OrdinalIgnoreCase).Count(); return language + (topics == 0 ? 0 : .5 * common / topics); }
}
