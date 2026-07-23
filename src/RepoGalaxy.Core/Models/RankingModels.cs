using RepoGalaxy.Core.Interfaces;

namespace RepoGalaxy.Core.Models;

public sealed record CandidateSet(IReadOnlyList<Repository> Items, string Source, DateTimeOffset BuiltAt);

public sealed record RankingContext(
    IReadOnlySet<string> PreferredLanguages,
    IReadOnlySet<string> PreferredTopics,
    IReadOnlySet<string> LocalLanguages,
    IReadOnlyDictionary<long, FeedbackType> Feedback,
    string BatchId,
    IReadOnlyDictionary<long, double>? StarVelocities = null,
    IReadOnlyDictionary<long, double>? RuleMatches = null,
    IReadOnlySet<string>? SuppressedSignals = null);

public sealed record FeatureVector(
    double RuleMatch,
    double Freshness,
    double StarVelocity,
    double Quality,
    double PreferenceAffinity,
    double Behavior,
    double Novelty,
    double LocalRelevance);

public sealed record CoarseRankResult(Repository Repository, FeatureVector Features, double Score);
public sealed record FineRankResult(Repository Repository, FeatureVector Features, double CoarseScore, double FineScore, bool IsExploration, int Position);
public sealed record RankingExplanation(string Summary, IReadOnlyList<string> Signals, string AlgorithmVersion = "heuristic-v1");
public sealed record RankedRecommendation(FineRankResult Result, RankingExplanation Explanation, string BatchId);

public enum RankingPreset { Balanced = 0, Precision = 1, Exploration = 2, Custom = 3 }

public sealed record CoarseRankingWeights(double RuleMatch, double Freshness, double StarVelocity, double Quality, double PreferenceAffinity)
{
    public double Total => RuleMatch + Freshness + StarVelocity + Quality + PreferenceAffinity;
}

public sealed record FineRankingWeights(double CoarseScore, double ContentProfile, double Behavior, double Novelty, double LocalRelevance)
{
    public double Total => CoarseScore + ContentProfile + Behavior + Novelty + LocalRelevance;
}

public sealed record RankingTuningProfile(
    string ScopeKey,
    RankingPreset Preset,
    CoarseRankingWeights Coarse,
    FineRankingWeights Fine,
    double ExplorationRatio,
    double Temperature,
    double FreshnessHalfLifeDays,
    int SameLanguagePerTen,
    int SameOwnerPerTen,
    int CoarseCandidateCount,
    int FineResultCount,
    int Revision = 1,
    DateTimeOffset? UpdatedAt = null)
{
    public bool IsValid => Math.Abs(Coarse.Total - 1) <= .0001 && Math.Abs(Fine.Total - 1) <= .0001
        && ExplorationRatio is >= 0 and <= .30 && Temperature is >= .25 and <= 2.5
        && FreshnessHalfLifeDays is >= 30 and <= 365 && SameLanguagePerTen is >= 1 and <= 5
        && SameOwnerPerTen is >= 1 and <= 3 && CoarseCandidateCount is >= 50 and <= 500
        && FineResultCount is >= 20 and <= 100;

    public static RankingTuningProfile Create(string scopeKey, RankingPreset preset) => preset switch
    {
        RankingPreset.Precision => new(scopeKey, preset, new(.35, .10, .15, .25, .15), new(.50, .25, .15, .05, .05), .05, .65, 120, 3, 1, 200, 60),
        RankingPreset.Exploration => new(scopeKey, preset, new(.20, .25, .20, .10, .25), new(.35, .20, .10, .20, .15), .25, 1.6, 90, 3, 1, 200, 60),
        _ => new(scopeKey, RankingPreset.Balanced, new(.30, .20, .20, .15, .15), new(.45, .20, .15, .10, .10), .15, 1, 120, 3, 1, 200, 60)
    };

    public RankingTuningProfile NormalizeWeights() => this with
    {
        Coarse = Normalize(Coarse),
        Fine = Normalize(Fine),
        Preset = RankingPreset.Custom
    };

    private static CoarseRankingWeights Normalize(CoarseRankingWeights value)
    {
        var total = value.Total;
        return total <= 0 ? Create("guest", RankingPreset.Balanced).Coarse : new(value.RuleMatch / total, value.Freshness / total, value.StarVelocity / total, value.Quality / total, value.PreferenceAffinity / total);
    }

    private static FineRankingWeights Normalize(FineRankingWeights value)
    {
        var total = value.Total;
        return total <= 0 ? Create("guest", RankingPreset.Balanced).Fine : new(value.CoarseScore / total, value.ContentProfile / total, value.Behavior / total, value.Novelty / total, value.LocalRelevance / total);
    }
}

public sealed record RankingRebuildRequest(string ScopeKey, FeedSource Source);
public sealed record RankingRebuildProgress(string Stage, double Progress, string Message);
public sealed record RankingRebuildResult(bool Success, bool Cancelled, string BatchId, IReadOnlyList<RankedRecommendation> Items, string? ErrorCode = null);
public sealed record RankingRebuiltEvent(string ScopeKey, FeedSource Source, string BatchId, IReadOnlyList<long> RepositoryIds);

public interface IRankingPipeline
{
    IReadOnlyList<CoarseRankResult> CoarseRank(CandidateSet candidates, RankingContext context, int count = 200);
    IReadOnlyList<FineRankResult> FineRank(IReadOnlyList<CoarseRankResult> candidates, RankingContext context, int count = 60);
    RankingExplanation Explain(FineRankResult result);
    IReadOnlyList<CoarseRankResult> CoarseRank(CandidateSet candidates, RankingContext context, RankingTuningProfile profile);
    IReadOnlyList<FineRankResult> FineRank(IReadOnlyList<CoarseRankResult> candidates, RankingContext context, RankingTuningProfile profile);
    RankingExplanation Explain(FineRankResult result, RankingTuningProfile profile);
}

public sealed record DashboardListItem(long RepositoryId, string FullName, string Caption, int Rank);
public sealed record ContributionDay(DateOnly Date, int Count);
public sealed record NewsArticle(long Id, string Title, string Summary, string Url, string Source, DateTimeOffset PublishedAt);
