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

    [Theory]
    [InlineData(RankingPreset.Balanced, .15, 1.0)]
    [InlineData(RankingPreset.Precision, .05, .65)]
    [InlineData(RankingPreset.Exploration, .25, 1.6)]
    public void Built_in_profiles_are_normalized_and_valid(RankingPreset preset, double exploration, double temperature)
    {
        var profile = RankingTuningProfile.Create("account", preset);
        profile.IsValid.Should().BeTrue();
        profile.Coarse.Total.Should().BeApproximately(1, .00001);
        profile.Fine.Total.Should().BeApproximately(1, .00001);
        profile.ExplorationRatio.Should().Be(exploration);
        profile.Temperature.Should().Be(temperature);
    }

    [Fact]
    public void Custom_coarse_weights_change_rank_without_changing_the_candidate_set()
    {
        var pipeline = new RankingPipeline();
        var repositories = new[] { Repository(1, "one", "C#"), Repository(2, "two", "C#") };
        var context = Context("weights", velocities: new Dictionary<long, double> { [1] = 0, [2] = 1 }, rules: new Dictionary<long, double> { [1] = 1, [2] = 0 });
        var baseline = RankingTuningProfile.Create("account", RankingPreset.Balanced);
        var ruleOnly = baseline with { Preset = RankingPreset.Custom, Coarse = new(1, 0, 0, 0, 0) };
        var velocityOnly = baseline with { Preset = RankingPreset.Custom, Coarse = new(0, 0, 1, 0, 0) };

        pipeline.CoarseRank(new(repositories, "test", DateTimeOffset.UtcNow), context, ruleOnly)[0].Repository.Id.Should().Be(1);
        pipeline.CoarseRank(new(repositories, "test", DateTimeOffset.UtcNow), context, velocityOnly)[0].Repository.Id.Should().Be(2);
    }

    [Fact]
    public void Temperature_sampling_is_stable_for_the_same_batch_and_profile_revision()
    {
        var pipeline = new RankingPipeline();
        var repositories = Enumerable.Range(1, 80).Select(i => Repository(i, $"owner-{i}", $"L{i % 12}")).ToList();
        var context = Context("temperature-stability");
        var profile = RankingTuningProfile.Create("account", RankingPreset.Exploration) with { Revision = 7 };
        var coarse = pipeline.CoarseRank(new(repositories, "test", DateTimeOffset.UtcNow), context, profile);

        pipeline.FineRank(coarse, context, profile).Select(x => x.Repository.Id)
            .Should().Equal(pipeline.FineRank(coarse, context, profile).Select(x => x.Repository.Id));
    }

    [Fact]
    public void Like_and_unlike_map_to_positive_and_neutral_behavior_features()
    {
        var pipeline = new RankingPipeline();
        var repository = Repository(1, "owner", "C#");
        var liked = Context("liked") with { Feedback = new Dictionary<long, FeedbackType> { [1] = FeedbackType.Like } };
        var unliked = Context("unliked") with { Feedback = new Dictionary<long, FeedbackType> { [1] = FeedbackType.Unlike } };

        pipeline.CoarseRank(new([repository], "test", DateTimeOffset.UtcNow), liked).Single().Features.Behavior.Should().Be(.9);
        pipeline.CoarseRank(new([repository], "test", DateTimeOffset.UtcNow), unliked).Single().Features.Behavior.Should().Be(.5);
    }

    [Fact]
    public void Language_and_topic_suppression_is_capped_at_fifty_percent()
    {
        var pipeline = new RankingPipeline();
        var repository = Repository(1, "owner", "C#");
        repository.Topics = ["avalonia", "desktop", "dotnet"];
        var baseline = Context("baseline");
        var suppressed = baseline with
        {
            SuppressedSignals = new HashSet<string>(
                ["language:c#", "topic:avalonia", "topic:desktop", "topic:dotnet"],
                StringComparer.OrdinalIgnoreCase)
        };
        var coarse = pipeline.CoarseRank(new([repository], "test", DateTimeOffset.UtcNow), baseline);
        var baselineScore = pipeline.FineRank(coarse, baseline).Single().FineScore;
        var suppressedScore = pipeline.FineRank(coarse, suppressed).Single().FineScore;

        suppressedScore.Should().BeApproximately(baselineScore * .5, .00001);
    }

    private static RankingContext Context(string batch, IReadOnlyDictionary<long, double>? velocities = null, IReadOnlyDictionary<long, double>? rules = null) =>
        new(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(["C#"], StringComparer.OrdinalIgnoreCase), new Dictionary<long, FeedbackType>(), batch, velocities, rules);
    private static Repository Repository(int id, string owner, string language) => new() { Id = id, GitHubId = $"node-{id}", Owner = owner, Name = $"repo-{id}", PrimaryLanguage = language, Stars = 1000 - id, Forks = 100, CreatedAt = DateTimeOffset.UtcNow.AddDays(-30), UpdatedAt = DateTimeOffset.UtcNow, Topics = [] };
}
