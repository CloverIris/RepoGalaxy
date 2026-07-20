using FluentAssertions;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Recommendation.Services;
using Xunit;

namespace RepoGalaxy.Recommendation.Tests;

public sealed class DiscoverySyncServiceTests
{
    [Fact]
    public void BuildQuery_combines_keyword_topic_and_first_language_rule()
    {
        var subscription = new DiscoverySubscription
        {
            Keywords = ["async runtime", "networking"],
            Topics = ["rust", "web"],
            Languages = ["Rust", "C#"]
        };

        DiscoverySyncService.BuildQuery(subscription).Should().Be("\"async runtime\" \"networking\" topic:rust topic:web language:Rust");
    }

    [Fact]
    public void BuildQuery_omits_blank_rules()
    {
        var subscription = new DiscoverySubscription { Keywords = ["  "], Topics = [""], Languages = [] };

        DiscoverySyncService.BuildQuery(subscription).Should().BeEmpty();
    }
}
