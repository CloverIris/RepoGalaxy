using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public sealed class ZoomableTileLayoutService : IZoomableTileLayoutService
{
    public const double MinimumZoom = .55;
    public const double DefaultZoom = 1;
    public const double MaximumZoom = 8;
    public const double SemanticFullyVisibleZoom = .55;
    public const double SemanticFadeEndZoom = .70;
    public ZoomScaleProfile ScaleProfile { get; } = new(MinimumZoom, SemanticFullyVisibleZoom, SemanticFadeEndZoom, DefaultZoom, MaximumZoom);

    public CameraState ZoomAt(CameraState camera, double requestedZoom, double anchorX, double anchorY, double viewportWidth, double viewportHeight, double worldWidth, double worldHeight)
    {
        var zoom = Math.Clamp(requestedZoom, MinimumZoom, MaximumZoom);
        var oldZoom = Math.Max(MinimumZoom, camera.Zoom);
        var worldAnchorX = camera.X + anchorX / oldZoom;
        var worldAnchorY = camera.Y + anchorY / oldZoom;
        var result = camera with { X = worldAnchorX - anchorX / zoom, Y = worldAnchorY - anchorY / zoom, Zoom = zoom };
        return result;
    }

    public CameraState Pan(CameraState camera, double screenDeltaX, double screenDeltaY, double viewportWidth, double viewportHeight, double worldWidth, double worldHeight)
    {
        var zoom = Math.Max(MinimumZoom, camera.Zoom);
        return camera with { X = camera.X - screenDeltaX / zoom, Y = camera.Y - screenDeltaY / zoom };
    }

    public CameraState CenterOn(CameraState camera, TileWorldRect target, double viewportWidth, double viewportHeight, double zoom)
    {
        zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        return camera with
        {
            X = target.CenterX - viewportWidth / zoom / 2,
            Y = target.CenterY - viewportHeight / zoom / 2,
            Zoom = zoom
        };
    }

    public double CalculateFitScale(TileWorldRect target, double viewportWidth, double viewportHeight)
    {
        if (target.Width <= 0 || target.Height <= 0 || viewportWidth <= 0 || viewportHeight <= 0) return DefaultZoom;
        var widthScale = Math.Max(1, viewportWidth - 64) / target.Width;
        var heightScale = Math.Max(1, viewportHeight - 64) / target.Height;
        return Math.Clamp(Math.Min(widthScale, heightScale), DefaultZoom, MaximumZoom);
    }

    public ZoomTransitionState CalculateTransition(CameraState camera, TileWorldRect? focusedTile, double viewportWidth, double viewportHeight)
    {
        var zoom = Math.Clamp(camera.Zoom, MinimumZoom, MaximumZoom);
        if (zoom <= SemanticFullyVisibleZoom) return new(ZoomVisualMode.SemanticIndex, 1, 0, 0, 1, false);
        if (zoom < SemanticFadeEndZoom)
        {
            var board = SmoothStep((zoom - SemanticFullyVisibleZoom) / (SemanticFadeEndZoom - SemanticFullyVisibleZoom));
            return new(ZoomVisualMode.SemanticIndex, 1 - board, board, 0, 1, false);
        }
        if (focusedTile is null) return new(ZoomVisualMode.TileBoard, 0, 1, 0, 1, false);

        var fit = CalculateFitScale(focusedTile.Value, viewportWidth, viewportHeight);
        var ratio = zoom / fit;
        var progress = SmoothStep(Math.Clamp((ratio - .65) / .35, 0, 1));
        var mode = progress >= .999 ? ZoomVisualMode.Detail : progress > 0 ? ZoomVisualMode.DetailTransition : ZoomVisualMode.TileBoard;
        return new(mode, 0, 1 - progress * .82, progress, fit, ratio >= .55);
    }

    private static double SmoothStep(double value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }
}
