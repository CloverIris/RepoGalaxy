using FluentAssertions;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Recommendation.Engine;
using Xunit;

namespace RepoGalaxy.Recommendation.Tests;

public sealed class RankingPipelineTests
{
    [Fact]
    public void Coarse_rank_uses_explicit_growth_and_rule_features()
    {
        var pipeline = new RankingPipeline();
        var repositories = new[] { Repository(1, "one", "C#"), Repository(2, "two", "C#") };
        var context = Context("batch", velocities: new Dictionary<long, double> { [1] = 0, [2] = 1 }, rules: new Dictionary<long, double> { [1] = 1, [2] = 0 });

        var ranked = pipeline.CoarseRank(new CandidateSet(repositories, "test", DateTimeOffset.UtcNow), context);

        ranked.Should().HaveCount(2);
        ranked.Single(x => x.Repository.Id == 1).Features.RuleMatch.Should().Be(1);
        ranked.Single(x => x.Repository.Id == 2).Features.StarVelocity.Should().Be(1);
    }

    [Fact]
    public void Fine_rank_is_stable_and_reserves_fifteen_percent_exploration()
    {
        var pipeline = new RankingPipeline();
        var repositories = Enumerable.Range(1, 60).Select(i => Repository(i, $"owner-{i}", i % 5 == 0 ? "Rust" : $"L{i % 9}")).ToList();
        var context = Context("stable-batch");
        var coarse = pipeline.CoarseRank(new CandidateSet(repositories, "test", DateTimeOffset.UtcNow), context, 200);

        var first = pipeline.FineRank(coarse, context, 60);
        var second = pipeline.FineRank(coarse, context, 60);

        first.Count(x => x.IsExploration).Should().Be(9);
        first.Select(x => x.Repository.Id).Should().Equal(second.Select(x => x.Repository.Id));
    }

    [Fact]
    public void Diversity_rerank_limits_language_and_owner_in_each_ten_item_window()
    {
        var pipeline = new RankingPipeline();
        var repositories = Enumerable.Range(1, 60).Select(i => Repository(i, $"owner-{i}", $"L{i % 8}")).ToList();
        var context = Context("diverse");
        var ranked = pipeline.FineRank(pipeline.CoarseRank(new CandidateSet(repositories, "test", DateTimeOffset.UtcNow), context), context, 60);

        foreach (var window in ranked.Chunk(10))
        {
            window.GroupBy(x => x.Repository.PrimaryLanguage).Max(x => x.Count()).Should().BeLessThanOrEqualTo(3);
            window.GroupBy(x => x.Repository.Owner).Max(x => x.Count()).Should().Be(1);
        }
    }

    [Fact]
    public void Explanation_marks_exploration_and_local_relevance()
    {
        var pipeline = new RankingPipeline();
        var repository = Repository(1, "owner", "C#");
        var features = new FeatureVector(.5, .8, .2, .7, .7, .5, 1, 1);
        var explanation = pipeline.Explain(new FineRankResult(repository, features, .6, .7, true, 1));

        explanation.Signals.Should().Contain(x => x.Contains("本地技术栈", StringComparison.Ordinal));
        explanation.Signals.Should().Contain(x => x.Contains("探索位", StringComparison.Ordinal));
    }

    private static RankingContext Context(string batch, IReadOnlyDictionary<long, double>? velocities = null, IReadOnlyDictionary<long, double>? rules = null) =>
        new(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(["C#"], StringComparer.OrdinalIgnoreCase), new Dictionary<long, FeedbackType>(), batch, velocities, rules);
    private static Repository Repository(int id, string owner, string language) => new() { Id = id, GitHubId = $"node-{id}", Owner = owner, Name = $"repo-{id}", PrimaryLanguage = language, Stars = 1000 - id, Forks = 100, CreatedAt = DateTimeOffset.UtcNow.AddDays(-30), UpdatedAt = DateTimeOffset.UtcNow, Topics = [] };
}
