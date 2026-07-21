namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// Viewport-sized Tile presenter. Real content is projected from an immutable
/// world snapshot while virtual skeleton chunks are batch-drawn by the base
/// renderer; the control itself never grows to the size of the infinite world.
/// </summary>
public sealed class VirtualizedTileWorldControl : ZoomableTileCanvas
{
}
