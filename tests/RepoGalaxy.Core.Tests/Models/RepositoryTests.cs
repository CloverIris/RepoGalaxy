using FluentAssertions;
using RepoGalaxy.Core.Models;
using Xunit;

namespace RepoGalaxy.Core.Tests.Models;

public class RepositoryTests
{
    [Fact]
    public void CalculateDiscoveryScore_rewards_recent_small_projects()
    {
        var fresh = new Repository { Stars = 30, Forks = 4, UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var stale = new Repository { Stars = 50000, Forks = 10, UpdatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        fresh.CalculateDiscoveryScore().Should().BeGreaterThan(stale.CalculateDiscoveryScore());
    }

    [Fact]
    public void Feed_item_keeps_source_and_explanation()
    {
        var item = new FeedItem { Source = FeedSource.Subscription, Reason = new FeedReason { Summary = "匹配 Rust", Score = .9 } };
        item.Source.Should().Be(FeedSource.Subscription);
        item.Reason.Summary.Should().Contain("Rust");
    }
}
