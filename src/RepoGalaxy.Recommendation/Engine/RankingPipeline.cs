using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Recommendation.Engine;

public sealed class RankingPipeline : IRankingPipeline
{
    public IReadOnlyList<CoarseRankResult> CoarseRank(CandidateSet candidates, RankingContext context, int count = 200) =>
        candidates.Items.Select(repo =>
        {
            var features = Features(repo, context);
            var score = features.RuleMatch * .30 + features.Freshness * .20 + features.StarVelocity * .20 + features.Quality * .15 + features.PreferenceAffinity * .15;
            return new CoarseRankResult(repo, features, Clamp(score));
        }).OrderByDescending(x => x.Score).ThenBy(x => x.Repository.FullName, StringComparer.OrdinalIgnoreCase).Take(count).ToList();

    public IReadOnlyList<FineRankResult> FineRank(IReadOnlyList<CoarseRankResult> candidates, RankingContext context, int count = 60)
    {
        var scored = candidates.Select(x => new ScoredCandidate(x, Clamp(x.Score * .45 + x.Features.PreferenceAffinity * .20 + x.Features.Behavior * .15 + x.Features.Novelty * .10 + x.Features.LocalRelevance * .10))).OrderByDescending(x => x.Score).ToList();
        var selected = new List<FineRankResult>();
        var pool = new List<ScoredCandidate>(scored);
        var random = new Random(StableSeed(context.BatchId));
        var finalCount = Math.Min(count, pool.Count);
        var explorationCount = (int)Math.Round(finalCount * .15, MidpointRounding.AwayFromZero);
        var explorationPositions = Enumerable.Range(1, explorationCount)
            .Select(index => (int)Math.Round(index * (finalCount + 1d) / (explorationCount + 1d), MidpointRounding.AwayFromZero))
            .ToHashSet();
        while (selected.Count < count && pool.Count > 0)
        {
            var position = selected.Count + 1;
            var exploration = explorationPositions.Contains(position) && pool.Count > 3;
            ScoredCandidate picked;
            var allowed = pool.Where(x => Allowed(selected, x.Coarse.Repository)).ToList();
            if (allowed.Count == 0) allowed = pool;
            if (exploration)
            {
                var explorationPool = allowed.Count > 5 ? allowed.Skip(5).Take(15).ToList() : allowed;
                var bound = explorationPool.Count;
                var lower = bound > 5 ? 5 : 0;
                picked = explorationPool[random.Next(lower, bound)];
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
    {
        var signals = new List<string>(); var f = result.Features;
        if (f.PreferenceAffinity >= .6) signals.Add("与你关注的语言和主题高度相关");
        if (f.StarVelocity >= .6) signals.Add("近期关注增长明显");
        if (f.Freshness >= .7) signals.Add("最近仍在活跃维护");
        if (f.LocalRelevance >= .6) signals.Add("与你的本地技术栈相关");
        if (result.IsExploration) signals.Add("探索位：扩展你的发现范围");
        if (signals.Count == 0) signals.Add("综合质量和活跃度表现稳定");
        return new RankingExplanation(signals[0], signals);
    }

    private static FeatureVector Features(Repository repo, RankingContext context)
    {
        var topicHits = repo.Topics.Count(x => context.PreferredTopics.Contains(x));
        var languageHit = context.PreferredLanguages.Contains(repo.PrimaryLanguage) ? 1d : 0d;
        var affinity = Clamp(languageHit * .55 + Math.Min(1, topicHits / 3d) * .45);
        var freshness = Math.Exp(-Math.Max(0, (DateTimeOffset.UtcNow - repo.UpdatedAt).TotalDays) / 120d);
        var velocity = context.StarVelocities?.GetValueOrDefault(repo.Id)
            ?? Clamp(Math.Log10(Math.Max(1, repo.Stars + 1)) / 5d * freshness);
        var quality = Clamp(Math.Log10(Math.Max(1, repo.Stars + repo.Forks * 2 + 1)) / 5d) * (repo.IsArchived ? .25 : 1);
        var feedback = context.Feedback.TryGetValue(repo.Id, out var action) ? action switch { FeedbackType.Bookmark => 1, FeedbackType.Click => .8, FeedbackType.View => .65, FeedbackType.Dismiss => .1, FeedbackType.Ignore => 0, _ => .5 } : .5;
        var novelty = context.Feedback.ContainsKey(repo.Id) ? .25 : 1;
        var local = context.LocalLanguages.Contains(repo.PrimaryLanguage) ? 1 : 0;
        var ruleMatch = context.RuleMatches?.GetValueOrDefault(repo.Id) ?? Math.Max(affinity, repo.DiscoveryScore);
        return new(Clamp(ruleMatch), freshness, Clamp(velocity), quality, affinity, feedback, novelty, local);
    }
    private static bool Allowed(IReadOnlyList<FineRankResult> selected, Repository candidate)
    {
        var window = selected.TakeLast(9).ToList();
        return window.Count(x => x.Repository.PrimaryLanguage.Equals(candidate.PrimaryLanguage, StringComparison.OrdinalIgnoreCase)) < 3
            && window.All(x => !x.Repository.Owner.Equals(candidate.Owner, StringComparison.OrdinalIgnoreCase));
    }
    private static int StableSeed(string value) { unchecked { var hash = 17; foreach (var c in value) hash = hash * 31 + c; return hash; } }
    private static double Clamp(double value) => Math.Clamp(value, 0, 1);
    private sealed record ScoredCandidate(CoarseRankResult Coarse, double Score);
}
