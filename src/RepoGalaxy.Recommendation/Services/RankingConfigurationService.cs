using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Recommendation.Services;

public sealed class RankingConfigurationService(IDbContextFactory<RepoGalaxyDbContext> factory) : IRankingConfigurationService
{
    public async Task<RankingTuningProfile> GetAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        scopeKey = NormalizeScope(scopeKey);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await db.RankingTuningProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.ScopeKey == scopeKey, cancellationToken);
        return entity is null ? RankingTuningProfile.Create(scopeKey, RankingPreset.Balanced) : Map(entity);
    }

    public async Task<RankingTuningProfile> SaveAsync(RankingTuningProfile profile, CancellationToken cancellationToken = default)
    {
        var normalized = profile with
        {
            ScopeKey = NormalizeScope(profile.ScopeKey),
            ExplorationRatio = Math.Clamp(profile.ExplorationRatio, 0, .30),
            Temperature = Math.Clamp(profile.Temperature, .25, 2.5),
            FreshnessHalfLifeDays = Math.Clamp(profile.FreshnessHalfLifeDays, 30, 365),
            SameLanguagePerTen = Math.Clamp(profile.SameLanguagePerTen, 1, 5),
            SameOwnerPerTen = Math.Clamp(profile.SameOwnerPerTen, 1, 3),
            CoarseCandidateCount = Math.Clamp(profile.CoarseCandidateCount, 50, 500),
            FineResultCount = Math.Clamp(profile.FineResultCount, 20, 100)
        };
        if (!normalized.IsValid) throw new ArgumentException("Ranking weights must each total 100%.", nameof(profile));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entity = await db.RankingTuningProfiles.SingleOrDefaultAsync(x => x.ScopeKey == normalized.ScopeKey, cancellationToken);
        if (entity is null)
        {
            entity = new RankingTuningProfileEntity { ScopeKey = normalized.ScopeKey };
            db.RankingTuningProfiles.Add(entity);
        }
        else normalized = normalized with { Revision = entity.Revision + 1 };

        Apply(entity, normalized with { UpdatedAt = DateTimeOffset.UtcNow });
        await db.RankingBatches.Where(x => x.AccountId == normalized.ScopeKey && !x.IsDirty).ExecuteUpdateAsync(x => x.SetProperty(b => b.IsDirty, true), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    private static string NormalizeScope(string value) => string.IsNullOrWhiteSpace(value) ? "guest" : value.Trim();

    private static RankingTuningProfile Map(RankingTuningProfileEntity value) => new(value.ScopeKey, (RankingPreset)value.Preset,
        new(value.CoarseRuleMatch, value.CoarseFreshness, value.CoarseStarVelocity, value.CoarseQuality, value.CoarsePreference),
        new(value.FineCoarse, value.FineContentProfile, value.FineBehavior, value.FineNovelty, value.FineLocalRelevance),
        value.ExplorationRatio, value.Temperature, value.FreshnessHalfLifeDays, value.SameLanguagePerTen,
        value.SameOwnerPerTen, value.CoarseCandidateCount, value.FineResultCount, value.Revision, value.UpdatedAt);

    private static void Apply(RankingTuningProfileEntity target, RankingTuningProfile value)
    {
        target.Preset = (int)value.Preset;
        target.CoarseRuleMatch = value.Coarse.RuleMatch; target.CoarseFreshness = value.Coarse.Freshness;
        target.CoarseStarVelocity = value.Coarse.StarVelocity; target.CoarseQuality = value.Coarse.Quality; target.CoarsePreference = value.Coarse.PreferenceAffinity;
        target.FineCoarse = value.Fine.CoarseScore; target.FineContentProfile = value.Fine.ContentProfile; target.FineBehavior = value.Fine.Behavior;
        target.FineNovelty = value.Fine.Novelty; target.FineLocalRelevance = value.Fine.LocalRelevance;
        target.ExplorationRatio = value.ExplorationRatio; target.Temperature = value.Temperature; target.FreshnessHalfLifeDays = value.FreshnessHalfLifeDays;
        target.SameLanguagePerTen = value.SameLanguagePerTen; target.SameOwnerPerTen = value.SameOwnerPerTen;
        target.CoarseCandidateCount = value.CoarseCandidateCount; target.FineResultCount = value.FineResultCount;
        target.Revision = value.Revision; target.UpdatedAt = value.UpdatedAt ?? DateTimeOffset.UtcNow;
    }
}
