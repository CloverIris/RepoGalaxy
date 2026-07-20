using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

public interface IMetroTileLayoutService
{
    Task<TileBoardState> LoadAsync(string scopeKey, FeedSource source, CancellationToken cancellationToken = default);
    Task<TileBoardState> SynchronizeAsync(string scopeKey, FeedSource source, IReadOnlyList<TileContent> content, int minimumColumns, int minimumRows, CancellationToken cancellationToken = default);
    Task SaveViewportAsync(long boardId, double x, double y, CancellationToken cancellationToken = default);
    Task ResetAsync(string? scopeKey = null, CancellationToken cancellationToken = default);
}

public interface ITilePaletteService
{
    TilePalette Create(string accentKey);
    double ContrastRatio(string first, string second);
}

public interface ITipCatalog
{
    IReadOnlyList<TipDefinition> GetTips(DateOnly date);
}

