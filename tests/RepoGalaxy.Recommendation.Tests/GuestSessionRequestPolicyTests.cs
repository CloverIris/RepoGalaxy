using FluentAssertions;
using RepoGalaxy.Recommendation.Services;
using Xunit;

namespace RepoGalaxy.Recommendation.Tests;

public sealed class GuestSessionRequestPolicyTests
{
    [Fact]
    public void Allows_only_the_first_automatic_request_but_all_manual_refreshes()
    {
        var policy = new GuestSessionRequestPolicy();
        policy.TryConsume(false).Should().BeTrue();
        policy.TryConsume(false).Should().BeFalse();
        policy.TryConsume(true).Should().BeTrue();
        policy.AutomaticRequestUsed.Should().BeTrue();
    }
}
