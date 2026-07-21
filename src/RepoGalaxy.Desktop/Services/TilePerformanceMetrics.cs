using System.Diagnostics.Metrics;

namespace RepoGalaxy.Desktop.Services;

internal static class TilePerformanceMetrics
{
    private static readonly Meter Meter = new("RepoGalaxy.TileWorld", "2.0.0");
    private static readonly Counter<long> Inputs = Meter.CreateCounter<long>("camera.inputs");
    private static readonly Counter<long> Frames = Meter.CreateCounter<long>("camera.frames");
    private static readonly Histogram<double> FrameDuration = Meter.CreateHistogram<double>("camera.frame.duration", "ms");
    private static readonly Histogram<int> Skeletons = Meter.CreateHistogram<int>("tile.skeleton.visible");
    private static readonly Counter<long> ChunkHits = Meter.CreateCounter<long>("tile.chunk.cache.hits");
    private static readonly Counter<long> ChunkMisses = Meter.CreateCounter<long>("tile.chunk.cache.misses");

    public static void InputQueued() => Inputs.Add(1);
    public static void FrameCommitted(double milliseconds) { Frames.Add(1); FrameDuration.Record(milliseconds); }
    public static void SkeletonCount(int value) => Skeletons.Record(value);
    public static void ChunkCache(bool hit) { if (hit) ChunkHits.Add(1); else ChunkMisses.Add(1); }
}
