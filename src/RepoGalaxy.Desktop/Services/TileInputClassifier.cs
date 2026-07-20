namespace RepoGalaxy.Desktop.Services;

public enum TileWheelIntent { Zoom, Pan }

public static class TileInputClassifier
{
    // Avalonia exposes precision-touchpad scrolling as high-resolution wheel deltas
    // on some Windows drivers. A lateral component or a fractional notch is treated
    // as a two-finger pan; a discrete vertical notch remains mouse-wheel zoom.
    public static TileWheelIntent Classify(double deltaX, double deltaY) =>
        Math.Abs(deltaX) > .001 || Math.Abs(deltaY) is > .001 and < .9 ? TileWheelIntent.Pan : TileWheelIntent.Zoom;
}
