using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Recommendation.Engine;

public sealed class RankingPipeline : IRankingPipeline
{
    public IReadOnlyList<CoarseRankResult> CoarseRank(CandidateSet candidates, RankingContext context, int count = 200) =>
        CoarseRank(candidates, context, RankingTuningProfile.Create("guest", RankingPreset.Balanced) with { CoarseCandidateCount = count });

    public IReadOnlyList<CoarseRankResult> CoarseRank(CandidateSet candidates, RankingContext context, RankingTuningProfile profile) =>
        candidates.Items.Select(repo =>
        {
            var features = Features(repo, context, profile.FreshnessHalfLifeDays);
            var weights = profile.Coarse;
            var score = features.RuleMatch * weights.RuleMatch + features.Freshness * weights.Freshness
                + features.StarVelocity * weights.StarVelocity + features.Quality * weights.Quality
                + features.PreferenceAffinity * weights.PreferenceAffinity;
            return new CoarseRankResult(repo, features, Clamp(score));
        }).OrderByDescending(x => x.Score).ThenBy(x => x.Repository.FullName, StringComparer.OrdinalIgnoreCase).Take(profile.CoarseCandidateCount).ToList();

    public IReadOnlyList<FineRankResult> FineRank(IReadOnlyList<CoarseRankResult> candidates, RankingContext context, int count = 60)
        => FineRank(candidates, context, RankingTuningProfile.Create("guest", RankingPreset.Balanced) with { FineResultCount = count });

    public IReadOnlyList<FineRankResult> FineRank(IReadOnlyList<CoarseRankResult> candidates, RankingContext context, RankingTuningProfile profile)
    {
        var weights = profile.Fine;
        var scored = candidates.Select(x => new ScoredCandidate(x, Clamp((x.Score * weights.CoarseScore
            + x.Features.PreferenceAffinity * weights.ContentProfile + x.Features.Behavior * weights.Behavior
            + x.Features.Novelty * weights.Novelty + x.Features.LocalRelevance * weights.LocalRelevance)
            * SuppressionMultiplier(x.Repository, context))))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Coarse.Repository.FullName, StringComparer.OrdinalIgnoreCase).ToList();
        var selected = new List<FineRankResult>();
        var pool = new List<ScoredCandidate>(scored);
        var random = new Random(StableSeed($"{context.BatchId}:{profile.Revision}"));
        var finalCount = Math.Min(profile.FineResultCount, pool.Count);
        var explorationCount = (int)Math.Round(finalCount * profile.ExplorationRatio, MidpointRounding.AwayFromZero);
        var explorationPositions = Enumerable.Range(1, explorationCount)
            .Select(index => (int)Math.Round(index * (finalCount + 1d) / (explorationCount + 1d), MidpointRounding.AwayFromZero))
            .ToHashSet();
        while (selected.Count < finalCount && pool.Count > 0)
        {
            var position = selected.Count + 1;
            var exploration = explorationPositions.Contains(position) && pool.Count > 3;
            ScoredCandidate picked;
            var allowed = pool.Where(x => Allowed(selected, x.Coarse.Repository, profile)).ToList();
            if (allowed.Count == 0) allowed = pool;
            if (exploration)
            {
                var explorationPool = allowed.Count > 5 ? allowed.Skip(Math.Min(5, allowed.Count - 1)).Take(30).ToList() : allowed;
                picked = SampleByTemperature(explorationPool, profile.Temperature, random);
            }
            else
            {
                picked = allowed[0];
            }
            pool.Remove(picked);
            selected.Add(new FineRankResult(picked.Coarse.Repository, picked.Coarse.Features, picked.Coarse.Score, picked.Score, exploration, position));
        }
        return selected;
    }

    public RankingExplanation Explain(FineRankResult result)
        => Explain(result, RankingTuningProfile.Create("guest", RankingPreset.Balanced));

    public RankingExplanation Explain(FineRankResult result, RankingTuningProfile profile)
    {
        var signals = new List<string>(); var f = result.Features;
        if (f.PreferenceAffinity >= .6) signals.Add("与你关注的语言和主题高度相关");
        if (f.StarVelocity >= .6) signals.Add("近期关注增长明显");
        if (f.Freshness >= .7) signals.Add("最近仍在活跃维护");
        if (f.LocalRelevance >= .6) signals.Add("与你的本地技术栈相关");
        if (result.IsExploration) signals.Add("探索位：扩展你的发现范围");
        if (signals.Count == 0) signals.Add("综合质量和活跃度表现稳定");
        return new RankingExplanation(signals[0], signals, $"heuristic-v2:r{profile.Revision}");
    }

    private static FeatureVector Features(Repository repo, RankingContext context, double freshnessHalfLifeDays)
    {
        var topicHits = repo.Topics.Count(x => context.PreferredTopics.Contains(x));
        var languageHit = context.PreferredLanguages.Contains(repo.PrimaryLanguage) ? 1d : 0d;
        var affinity = Clamp(languageHit * .55 + Math.Min(1, topicHits / 3d) * .45);
        var freshness = Math.Pow(.5, Math.Max(0, (DateTimeOffset.UtcNow - repo.UpdatedAt).TotalDays) / Math.Max(1, freshnessHalfLifeDays));
        var velocity = context.StarVelocities?.GetValueOrDefault(repo.Id)
            ?? Clamp(Math.Log10(Math.Max(1, repo.Stars + 1)) / 5d * freshness);
        var quality = Clamp(Math.Log10(Math.Max(1, repo.Stars + repo.Forks * 2 + 1)) / 5d) * (repo.IsArchived ? .25 : 1);
        var feedback = context.Feedback.TryGetValue(repo.Id, out var action) ? action switch { FeedbackType.Bookmark => 1, FeedbackType.Like => .9, FeedbackType.Click => .8, FeedbackType.View => .65, FeedbackType.Dismiss => .1, FeedbackType.Ignore => 0, FeedbackType.Unlike => .5, _ => .5 } : .5;
        var novelty = context.Feedback.ContainsKey(repo.Id) ? .25 : 1;
        var local = context.LocalLanguages.Contains(repo.PrimaryLanguage) ? 1 : 0;
        var ruleMatch = context.RuleMatches?.GetValueOrDefault(repo.Id) ?? Math.Max(affinity, repo.DiscoveryScore);
        return new(Clamp(ruleMatch), freshness, Clamp(velocity), quality, affinity, feedback, novelty, local);
    }
    private static bool Allowed(IReadOnlyList<FineRankResult> selected, Repository candidate, RankingTuningProfile profile)
    {
        var window = selected.TakeLast(9).ToList();
        return window.Count(x => x.Repository.PrimaryLanguage.Equals(candidate.PrimaryLanguage, StringComparison.OrdinalIgnoreCase)) < profile.SameLanguagePerTen
            && window.Count(x => x.Repository.Owner.Equals(candidate.Owner, StringComparison.OrdinalIgnoreCase)) < profile.SameOwnerPerTen;
    }
    private static double SuppressionMultiplier(Repository repository, RankingContext context)
    {
        if (context.SuppressedSignals is not { Count: > 0 } signals) return 1;
        var reduction = signals.Contains($"language:{NormalizeSignal(repository.PrimaryLanguage)}") ? .30 : 0;
        reduction += repository.Topics.Count(topic => signals.Contains($"topic:{NormalizeSignal(topic)}")) * .10;
        return 1 - Math.Min(.50, reduction);
    }
    private static string NormalizeSignal(string value) =>
        string.Join('-', (value ?? string.Empty).Trim().ToLowerInvariant().Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries));
    private static ScoredCandidate SampleByTemperature(IReadOnlyList<ScoredCandidate> pool, double temperature, Random random)
    {
        if (pool.Count == 1) return pool[0];
        var max = pool.Max(x => x.Score);
        var weights = pool.Select(x => Math.Exp((x.Score - max) / Math.Max(.25, temperature))).ToArray();
        var target = random.NextDouble() * weights.Sum();
        for (var i = 0; i < pool.Count; i++) { target -= weights[i]; if (target <= 0) return pool[i]; }
        return pool[^1];
    }
    private static int StableSeed(string value) { unchecked { var hash = 17; foreach (var c in value) hash = hash * 31 + c; return hash; } }
    private static double Clamp(double value) => Math.Clamp(value, 0, 1);
    private sealed record ScoredCandidate(CoarseRankResult Coarse, double Score);
}
