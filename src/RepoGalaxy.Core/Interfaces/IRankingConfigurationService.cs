using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

public interface IRankingConfigurationService
{
    Task<RankingTuningProfile> GetAsync(string scopeKey, CancellationToken cancellationToken = default);
    Task<RankingTuningProfile> SaveAsync(RankingTuningProfile profile, CancellationToken cancellationToken = default);
}

public interface IRankingRebuildService
{
    event EventHandler<RankingRebuiltEvent>? Rebuilt;
    Task<RankingRebuildResult> RebuildAsync(RankingRebuildRequest request, IProgress<RankingRebuildProgress>? progress = null, CancellationToken cancellationToken = default);
}
