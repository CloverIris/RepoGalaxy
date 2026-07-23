using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

public interface IRepositoryTileActionService
{
    Task<IReadOnlyDictionary<long, RepositoryTileActionState>> LoadStatesAsync(
        IReadOnlyCollection<long> repositoryIds,
        CancellationToken cancellationToken = default);

    Task<bool> SetLikedAsync(long repositoryId, bool liked, CancellationToken cancellationToken = default);

    Task<RepositoryStarResult> ToggleStarAsync(
        Repository repository,
        bool currentlyStarred,
        CancellationToken cancellationToken = default);

    Task<CategorySuppressionResult> SuppressCategoryAsync(
        Repository repository,
        IReadOnlyCollection<Repository> currentFeed,
        CancellationToken cancellationToken = default);
}
