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
    IReadOnlyDictionary<long, double>? RuleMatches = null);

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

public interface IRankingPipeline
{
    IReadOnlyList<CoarseRankResult> CoarseRank(CandidateSet candidates, RankingContext context, int count = 200);
    IReadOnlyList<FineRankResult> FineRank(IReadOnlyList<CoarseRankResult> candidates, RankingContext context, int count = 60);
    RankingExplanation Explain(FineRankResult result);
}

public sealed record DashboardListItem(long RepositoryId, string FullName, string Caption, int Rank);
public sealed record ContributionDay(DateOnly Date, int Count);
public sealed record NewsArticle(long Id, string Title, string Summary, string Url, string Source, DateTimeOffset PublishedAt);
