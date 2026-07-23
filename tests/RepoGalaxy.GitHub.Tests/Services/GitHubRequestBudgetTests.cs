using FluentAssertions;
using RepoGalaxy.Core.Models;
using RepoGalaxy.GitHub.Services;
using Xunit;

namespace RepoGalaxy.GitHub.Tests.Services;

public sealed class GitHubRequestBudgetTests
{
    [Fact]
    public void Core_and_search_windows_are_independent_and_use_reported_used_values()
    {
        var budget = new GitHubRequestBudget();
        budget.BeginSession(GitHubBudgetSessionKind.Authenticated, "account-42");
        var observedAt = DateTimeOffset.UtcNow;

        budget.Update(new GitHubRateWindow("core", 5000, 4875, observedAt.AddHours(1), 125, observedAt));
        budget.Update(new GitHubRateWindow("search", 30, 7, observedAt.AddMinutes(1), 23, observedAt));

        budget.Snapshot.ScopeKey.Should().Be("account-42");
        budget.Snapshot.Core!.EffectiveUsed.Should().Be(125);
        budget.Snapshot.Search!.EffectiveUsed.Should().Be(23);
        budget.Snapshot.Core.UsedRatio.Should().BeApproximately(.025, .0001);
        budget.Snapshot.Search.UsedRatio.Should().BeApproximately(23d / 30d, .0001);
    }

    [Fact]
    public void Starting_a_new_scope_does_not_leak_previous_account_windows()
    {
        var budget = new GitHubRequestBudget();
        budget.BeginSession(GitHubBudgetSessionKind.Authenticated, "first");
        budget.Update(new GitHubRateWindow("core", 5000, 4990, DateTimeOffset.UtcNow.AddHours(1)));

        budget.BeginSession(GitHubBudgetSessionKind.Guest, "guest");

        budget.Snapshot.SessionKind.Should().Be(GitHubBudgetSessionKind.Guest);
        budget.Snapshot.ScopeKey.Should().Be("guest");
        budget.Snapshot.Core.Should().BeNull();
        budget.Snapshot.Search.Should().BeNull();
    }

    [Fact]
    public void Exhausted_resource_blocks_only_its_own_queue_until_reset()
    {
        var budget = new GitHubRequestBudget();
        var reset = DateTimeOffset.UtcNow.AddMinutes(12);
        budget.Update(new GitHubRateWindow("search", 30, 0, reset));
        budget.Update(new GitHubRateWindow("core", 60, 50, reset));

        budget.CanSearch(out var searchReset).Should().BeFalse();
        searchReset.Should().Be(reset);
        budget.CanCore(out _).Should().BeTrue();
    }

    [Fact]
    public void Graphql_window_is_independent_from_core_and_search()
    {
        var budget = new GitHubRequestBudget();
        var reset = DateTimeOffset.UtcNow.AddHours(1);

        budget.Update(new GitHubRateWindow("core", 5000, 4900, reset));
        budget.Update(new GitHubRateWindow("search", 30, 20, reset));
        budget.Update(new GitHubRateWindow("graphql", 5000, 4800, reset));

        budget.Snapshot.Core!.Remaining.Should().Be(4900);
        budget.Snapshot.Search!.Remaining.Should().Be(20);
        budget.Snapshot.GraphQl!.Remaining.Should().Be(4800);
    }
}
