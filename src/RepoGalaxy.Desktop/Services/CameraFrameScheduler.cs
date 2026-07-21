using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RepoGalaxy.Desktop.Services;

/// <summary>
/// Coalesces high-frequency camera input so the view model is updated at most once
/// for each Avalonia render frame.
/// </summary>
public interface ICameraFrameScheduler
{
    bool IsScheduled { get; }
    void Schedule(Action<TimeSpan> callback);
}

public sealed class AvaloniaCameraFrameScheduler(Control owner) : ICameraFrameScheduler
{
    private Action<TimeSpan>? _pending;
    public bool IsScheduled { get; private set; }

    public void Schedule(Action<TimeSpan> callback)
    {
        _pending = callback;
        if (IsScheduled) return;
        IsScheduled = true;
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel is not null)
        {
            topLevel.RequestAnimationFrame(Flush);
            return;
        }

        Dispatcher.UIThread.Post(() => Flush(TimeSpan.Zero), DispatcherPriority.Render);
    }

    private void Flush(TimeSpan timestamp)
    {
        IsScheduled = false;
        var callback = Interlocked.Exchange(ref _pending, null);
        callback?.Invoke(timestamp);
    }
}

public readonly record struct CameraInputFrame(double PanX, double PanY, double WheelDelta, double ZoomFactor, Point Anchor, object? Target, int InputCount)
{
    public bool IsEmpty => InputCount == 0;
}

public sealed class CameraInputAccumulator
{
    private double _panX;
    private double _panY;
    private double _wheel;
    private double _factor = 1;
    private Point _anchor;
    private object? _target;
    private int _inputCount;

    public void AddPan(double x, double y) { _panX += x; _panY += y; _inputCount++; }
    public void AddWheel(double value, Point anchor, object? target) { _wheel += value; _anchor = anchor; _target = target; _inputCount++; }
    public void AddZoomFactor(double value, Point anchor, object? target) { _factor *= Math.Clamp(value, .72, 1.38); _anchor = anchor; _target = target; _inputCount++; }

    public CameraInputFrame Drain()
    {
        var result = new CameraInputFrame(_panX, _panY, _wheel, _factor, _anchor, _target, _inputCount);
        _panX = _panY = _wheel = 0; _factor = 1; _anchor = default; _target = null; _inputCount = 0;
        return result;
    }
}
