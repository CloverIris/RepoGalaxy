using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Recommendation.Services;
using Xunit;

namespace RepoGalaxy.Recommendation.Tests;

public sealed class RankingConfigurationServiceTests
{
    [Fact]
    public async Task Profiles_are_isolated_by_account_and_saving_marks_only_that_accounts_batches_dirty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite(connection).Options;
        var factory = new TestFactory(options);
        await using (var database = factory.CreateDbContext())
        {
            await database.Database.EnsureCreatedAsync();
            database.RankingBatches.AddRange(
                new RankingBatchEntity { BatchId = "guest-batch", AccountId = "guest", Source = "ForYou", CreatedAt = DateTimeOffset.UtcNow },
                new RankingBatchEntity { BatchId = "account-batch", AccountId = "42", Source = "ForYou", CreatedAt = DateTimeOffset.UtcNow });
            await database.SaveChangesAsync();
        }
        var service = new RankingConfigurationService(factory);

        var saved = await service.SaveAsync(RankingTuningProfile.Create("42", RankingPreset.Precision));
        var guest = await service.GetAsync("guest");

        saved.Preset.Should().Be(RankingPreset.Precision);
        guest.Preset.Should().Be(RankingPreset.Balanced);
        await using var verification = factory.CreateDbContext();
        (await verification.RankingBatches.SingleAsync(x => x.AccountId == "42")).IsDirty.Should().BeTrue();
        (await verification.RankingBatches.SingleAsync(x => x.AccountId == "guest")).IsDirty.Should().BeFalse();
    }

    private sealed class TestFactory(DbContextOptions<RepoGalaxyDbContext> options) : IDbContextFactory<RepoGalaxyDbContext>
    {
        public RepoGalaxyDbContext CreateDbContext() => new(options);
    }
}
