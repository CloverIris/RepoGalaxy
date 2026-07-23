using System.Diagnostics.Metrics;

namespace RepoGalaxy.Desktop.Services;

internal static class TilePerformanceMetrics
{
    private static readonly Meter Meter = new("RepoGalaxy.TileWorld", "2.0.0");
    private static readonly Counter<long> Inputs = Meter.CreateCounter<long>("camera.inputs");
    private static readonly Counter<long> Frames = Meter.CreateCounter<long>("camera.frames");
    private static readonly Histogram<double> FrameDuration = Meter.CreateHistogram<double>("camera.frame.duration", "ms");
    private static readonly Histogram<int> Skeletons = Meter.CreateHistogram<int>("tile.skeleton.visible");
    private static readonly Histogram<int> MaterializedControls = Meter.CreateHistogram<int>("tile.controls.materialized");
    private static readonly Counter<long> ChunkHits = Meter.CreateCounter<long>("tile.chunk.cache.hits");
    private static readonly Counter<long> ChunkMisses = Meter.CreateCounter<long>("tile.chunk.cache.misses");
    private static readonly Histogram<long> FrameAllocations = Meter.CreateHistogram<long>("tile.frame.allocations", "bytes");
    private static readonly Counter<long> Gen0Collections = Meter.CreateCounter<long>("tile.gc.gen0");
    private static readonly Counter<long> Gen1Collections = Meter.CreateCounter<long>("tile.gc.gen1");
    private static readonly Counter<long> Gen2Collections = Meter.CreateCounter<long>("tile.gc.gen2");

    public static void InputQueued() => Inputs.Add(1);
    public static void FrameCommitted(double milliseconds) { Frames.Add(1); FrameDuration.Record(milliseconds); }
    public static void SkeletonCount(int value) => Skeletons.Record(value);
    public static void MaterializedControlCount(int value) => MaterializedControls.Record(value);
    public static void ChunkCache(bool hit) { if (hit) ChunkHits.Add(1); else ChunkMisses.Add(1); }
    public static void Allocation(long bytes, int gen0, int gen1, int gen2)
    {
        FrameAllocations.Record(Math.Max(0, bytes));
        if (gen0 > 0) Gen0Collections.Add(gen0);
        if (gen1 > 0) Gen1Collections.Add(gen1);
        if (gen2 > 0) Gen2Collections.Add(gen2);
    }
}
