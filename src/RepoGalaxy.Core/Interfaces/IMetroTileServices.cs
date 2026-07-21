using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

public interface IMetroTileLayoutService
{
    Task<TileBoardState> LoadAsync(string scopeKey, FeedSource source, CancellationToken cancellationToken = default);
    Task<TileBoardState> SynchronizeAsync(string scopeKey, FeedSource source, IReadOnlyList<TileContent> content, int minimumColumns, int minimumRows, bool reflow = false, CancellationToken cancellationToken = default);
    Task<TileBoardState> ReorderRepositoriesAsync(long boardId, IReadOnlyList<long> repositoryIds, TileWorldWindow preferredWindow, CancellationToken cancellationToken = default);
    Task SaveCameraAsync(long boardId, CameraState camera, CancellationToken cancellationToken = default);
    Task SaveSemanticViewportAsync(long boardId, SemanticViewportState viewport, CancellationToken cancellationToken = default);
    Task ResetAsync(string? scopeKey = null, CancellationToken cancellationToken = default);
}

public interface ISemanticIndexCatalogService
{
    SemanticIndexCatalogResult Build(IReadOnlyList<SemanticIndexSignal> signals, SemanticIndexPolicy? policy = null);
}

public interface ISpatialTileSearchService
{
    SpatialTileSearchResult Search(string query, IReadOnlyList<TileSearchCandidate> candidates, double cameraCenterX, double cameraCenterY);
}

public interface IVirtualTileWorldService
{
    IReadOnlyList<VirtualTileSlot> Materialize(string boardSeed, TileWorldWindow window, IReadOnlyList<TilePlacement> persistentPlacements, IReadOnlyList<TipDefinition> tips);
    (int Column, int Row) FindNearestCompatibleSlot(string boardSeed, TileWorldWindow preferredWindow, TileSpan span, IReadOnlyList<TilePlacement> persistentPlacements);
}

public interface ITileWorldPresentationService
{
    TileWorldSnapshot CreateSnapshot(TileBoardState board, string? anchorContentKey = null);
    IReadOnlyList<TilePlacement> QueryVisible(TileWorldSnapshot snapshot, TileWorldViewport viewport, double overscan = 180);
}

public interface ISemanticMosaicLayoutService
{
    Task<SemanticMosaicState> SynchronizeAsync(long boardId, IReadOnlyList<SemanticIndexItem> items, double targetAspect, CancellationToken cancellationToken = default);
    Task ResetAsync(long boardId, CancellationToken cancellationToken = default);
}

public interface IZoomableTileLayoutService
{
    ZoomScaleProfile ScaleProfile { get; }
    CameraState ZoomAt(CameraState camera, double requestedZoom, double anchorX, double anchorY, double viewportWidth, double viewportHeight, double worldWidth, double worldHeight);
    CameraState Pan(CameraState camera, double screenDeltaX, double screenDeltaY, double viewportWidth, double viewportHeight, double worldWidth, double worldHeight);
    CameraState CenterOn(CameraState camera, TileWorldRect target, double viewportWidth, double viewportHeight, double zoom);
    double CalculateFitScale(TileWorldRect target, double viewportWidth, double viewportHeight);
    ZoomTransitionState CalculateTransition(CameraState camera, TileWorldRect? focusedTile, double viewportWidth, double viewportHeight);
}

public interface IDetailPortalCoordinator
{
    DetailPortalDecision Evaluate(DetailPresentationState current, bool hasFocus, double fitRatio);
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

public interface IDetailContentService
{
    DetailSnapshot CreateBaseline(DetailTarget target);
    Task<DetailSnapshot> LoadAsync(DetailTarget target, CancellationToken cancellationToken = default);
}

public interface IExternalMetadataExtractor
{
    Task<ExternalMetadata?> ExtractAsync(string url, CancellationToken cancellationToken = default);
}

public interface IMarkdownDocumentService
{
    MarkdownDocument Parse(string markdown, string title, int pageCapacity = 28, string baseUrl = "");
}

public interface ISafeMarkdownImageService
{
    Task<SafeImageResult?> GetAsync(string url, long documentBudgetBytes, CancellationToken cancellationToken = default);
}

public interface ILocalIdeDiscoveryService
{
    Task<IReadOnlyList<LocalIdeDescriptor>> DiscoverAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
    LocalIdeDescriptor? Recommend(IReadOnlyList<LocalIdeDescriptor> candidates, string language, IReadOnlyList<string> topics);
}

public interface ILocalRepositoryResolver
{
    Task<LocalRepository?> ResolveAsync(long repositoryId, string owner, string name, string cloneUrl, CancellationToken cancellationToken = default);
    Task<string?> ReadOriginAsync(string repositoryPath, CancellationToken cancellationToken = default);
}

public interface IRepositoryCloneService
{
    Task<CloneResult> CloneAsync(CloneRequest request, IProgress<CloneProgress>? progress = null, CancellationToken cancellationToken = default);
    Task CleanupAbandonedAsync(CancellationToken cancellationToken = default);
}

public interface IIdeLauncher
{
    Task<(bool Success, string ErrorCode)> OpenAsync(LocalIdeDescriptor ide, string repositoryPath, CancellationToken cancellationToken = default);
}

public interface IIdePreferenceService
{
    Task<string?> GetPreferredIdeAsync(string scopeKey, string technologyKey, CancellationToken cancellationToken = default);
    Task SetPreferredIdeAsync(string scopeKey, string technologyKey, string ideKey, CancellationToken cancellationToken = default);
}
