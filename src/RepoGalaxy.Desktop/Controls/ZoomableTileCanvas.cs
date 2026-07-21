using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.Media;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Desktop.Services;
using System.Globalization;
using System.Diagnostics;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// Projects a fixed-size tile world through a camera. Pointer wheel changes only Z;
/// mouse drag and scroll gestures change only X/Y.
/// </summary>
public sealed class ZoomableTileCanvas : ContentControl
{
    public static readonly StyledProperty<double> CameraXProperty =
        AvaloniaProperty.Register<ZoomableTileCanvas, double>(nameof(CameraX));
    public static readonly StyledProperty<double> CameraYProperty =
        AvaloniaProperty.Register<ZoomableTileCanvas, double>(nameof(CameraY));
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<ZoomableTileCanvas, double>(nameof(Zoom), 1d);
    public static readonly StyledProperty<int> SkeletonRevisionProperty =
        AvaloniaProperty.Register<ZoomableTileCanvas, int>(nameof(SkeletonRevision));

    private Point _pressPoint;
    private bool _mousePressed;
    private bool _mouseDragging;
    private MetroTileViewModel? _pressedTile;
    private readonly MatrixTransform _worldTransform = new();
    private readonly ICameraFrameScheduler _frameScheduler;
    private readonly CameraInputAccumulator _input = new();
    private readonly Dictionary<(string Key, int Size, uint Color), FormattedText> _textCache = [];

    public double CameraX { get => GetValue(CameraXProperty); set => SetValue(CameraXProperty, value); }
    public double CameraY { get => GetValue(CameraYProperty); set => SetValue(CameraYProperty, value); }
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public int SkeletonRevision { get => GetValue(SkeletonRevisionProperty); set => SetValue(SkeletonRevisionProperty, value); }

    public ZoomableTileCanvas()
    {
        _frameScheduler = new AvaloniaCameraFrameScheduler(this);
        Focusable = true;
        ClipToBounds = true;
        GestureRecognizers.Add(new ScrollGestureRecognizer
        {
            CanHorizontallyScroll = true,
            CanVerticallyScroll = true,
            IsScrollInertiaEnabled = true
        });

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.ScrollGestureEvent, OnScrollGesture);
        AddHandler(InputElement.ScrollGestureEndedEvent, OnScrollGestureEnded);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CameraXProperty || change.Property == CameraYProperty || change.Property == ZoomProperty)
        {
            ApplyProjection();
            InvalidateVisual();
        }
        else if (change.Property == SkeletonRevisionProperty)
        {
            _textCache.Clear();
            InvalidateVisual();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ApplyProjection();
    }

    private void ApplyProjection()
    {
        if (Content is not Visual content) return;
        content.RenderTransformOrigin = RelativePoint.TopLeft;
        content.RenderTransform = _worldTransform;
        _worldTransform.Matrix = new Matrix(Zoom, 0, 0, Zoom, -CameraX * Zoom, -CameraY * Zoom);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DataContext is not DiscoverViewModel vm || vm.RenderedSkeletonSlots.Count == 0) return;

        var viewport = new Rect(Bounds.Size);
        foreach (var slot in vm.RenderedSkeletonSlots)
        {
            var left = (slot.Column * 100d - CameraX) * Zoom;
            var top = (slot.Row * 100d - CameraY) * Zoom;
            var width = (slot.ColumnSpan * 96d + (slot.ColumnSpan - 1) * 4d) * Zoom;
            var height = (slot.RowSpan * 96d + (slot.RowSpan - 1) * 4d) * Zoom;
            var bounds = new Rect(left, top, width, height);
            if (!viewport.Intersects(bounds)) continue;

            var color = SkeletonColor(slot.Content.AccentKey);
            var fill = new SolidColorBrush(Color.FromArgb(36, color.R, color.G, color.B));
            var border = new Pen(new SolidColorBrush(Color.FromArgb(105, color.R, color.G, color.B)), 1);
            context.DrawRectangle(fill, border, bounds);

            if (width < 64 || height < 44) continue;
            var fontSize = Math.Clamp((int)Math.Round(12 * Zoom), 9, 18);
            var foreground = RelativeLuminance(color) > .42 ? Colors.Black : Colors.White;
            var text = GetText(slot.Content.Key, slot.Content.Title, fontSize, foreground);
            using (context.PushClip(bounds.Deflate(Math.Max(4, 9 * Zoom))))
                context.DrawText(text, new Point(left + Math.Max(5, 9 * Zoom), top + Math.Max(5, 9 * Zoom)));
        }
    }

    public void QueuePan(double screenDeltaX, double screenDeltaY)
    {
        TilePerformanceMetrics.InputQueued();
        _input.AddPan(screenDeltaX, screenDeltaY);
        ScheduleCameraFrame();
    }

    public void QueueZoom(double wheelDelta, double anchorX, double anchorY, MetroTileViewModel? tile = null)
    {
        TilePerformanceMetrics.InputQueued();
        _input.AddWheel(wheelDelta, new(anchorX, anchorY), tile);
        ScheduleCameraFrame();
    }

    public void QueueZoomFactor(double factor, double anchorX, double anchorY, MetroTileViewModel? tile = null)
    {
        TilePerformanceMetrics.InputQueued();
        _input.AddZoomFactor(factor, new(anchorX, anchorY), tile);
        ScheduleCameraFrame();
    }

    private void ScheduleCameraFrame() => _frameScheduler.Schedule(_ =>
    {
        var stopwatch = Stopwatch.StartNew();
        if (DataContext is not DiscoverViewModel vm) return;
        var input = _input.Drain();
        var tile = input.Target as MetroTileViewModel;
        if (input.PanX != 0 || input.PanY != 0) vm.PanBy(input.PanX, input.PanY);
        if (input.WheelDelta != 0) vm.ZoomBy(input.WheelDelta, input.Anchor.X, input.Anchor.Y, tile);
        if (Math.Abs(input.ZoomFactor - 1) > .0001) vm.ZoomByFactor(input.ZoomFactor, input.Anchor.X, input.Anchor.Y, tile);
        vm.CommitCamera();
        TilePerformanceMetrics.FrameCommitted(stopwatch.Elapsed.TotalMilliseconds);
    });

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) || IsActionControl(e.Source)) return;
        Focus();
        _pressPoint = e.GetPosition(this);
        _mousePressed = true;
        _mouseDragging = false;
        _pressedTile = FindTile(e.Source);
        if (DataContext is DiscoverViewModel vm) vm.BeginTilePointerInteraction(_pressedTile);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_mousePressed || DataContext is not DiscoverViewModel vm) return;
        var current = e.GetPosition(this);
        var delta = current - _pressPoint;
        if (!_mouseDragging && Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) < 6) return;
        if (!_mouseDragging)
        {
            _mouseDragging = true;
            vm.MarkTilePointerDragging();
        }
        QueuePan(delta.X, delta.Y);
        _pressPoint = current;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_mousePressed) return;
        var wasDragging = _mouseDragging;
        var tile = _pressedTile;
        _mousePressed = false;
        if (DataContext is DiscoverViewModel vm) { vm.EndTilePointerInteraction(); vm.CommitCamera(); }
        if (!wasDragging && DataContext is DiscoverViewModel discover) discover.ActivateTileFromPointer(tile);
        e.Pointer.Capture(null);
        e.Handled = true;
        _mouseDragging = false;
        _pressedTile = null;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (DataContext is DiscoverViewModel vm) vm.EndTilePointerInteraction();
        _mousePressed = false;
        _mouseDragging = false;
        _pressedTile = null;
    }

    private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
    {
        if (_mousePressed || DataContext is not DiscoverViewModel vm) return;
        QueuePan(-e.Delta.X, -e.Delta.Y);
        e.Handled = true;
    }

    private void OnScrollGestureEnded(object? sender, ScrollGestureEndedEventArgs e)
    {
        if (DataContext is DiscoverViewModel vm) vm.CommitCamera();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not DiscoverViewModel vm) return;
        vm.HandleKey(e.Key.ToString());
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Add or Key.Subtract or Key.OemPlus or Key.OemMinus or Key.Home or Key.Escape)
            e.Handled = true;
    }

    private static bool IsActionControl(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control is Button button && !button.Classes.Contains("tile-hit") || control is TextBox or ComboBox or ToggleSwitch) return true;
        return false;
    }

    private static MetroTileViewModel? FindTile(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control.DataContext is MetroTileViewModel tile) return tile;
        return null;
    }

    private FormattedText GetText(string key, string value, int size, Color color)
    {
        var cacheKey = (key, size, color.ToUInt32());
        if (_textCache.TryGetValue(cacheKey, out var text)) return text;
        if (_textCache.Count > 768) _textCache.Clear();
        text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI, Microsoft YaHei UI"), size, new SolidColorBrush(color));
        _textCache[cacheKey] = text;
        return text;
    }

    private static Color SkeletonColor(string value)
    {
        var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(value ?? string.Empty);
        var hue = Math.Abs(hash % 360);
        var c = .58;
        var x = c * (1 - Math.Abs(hue / 60d % 2 - 1));
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
            < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x)
        };
        const double m = .16;
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    private static double RelativeLuminance(Color color)
    {
        static double Linear(byte component) { var c = component / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }
}
