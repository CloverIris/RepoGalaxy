using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.Media;
using RepoGalaxy.Desktop.ViewModels;

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

    private Point _pressPoint;
    private bool _mousePressed;
    private bool _mouseDragging;
    private MetroTileViewModel? _pressedTile;

    public double CameraX { get => GetValue(CameraXProperty); set => SetValue(CameraXProperty, value); }
    public double CameraY { get => GetValue(CameraYProperty); set => SetValue(CameraYProperty, value); }
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    public ZoomableTileCanvas()
    {
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
            ApplyProjection();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ApplyProjection();
    }

    private void ApplyProjection()
    {
        if (Content is not Visual content) return;
        var zoom = Math.Max(.01, Zoom);
        content.RenderTransformOrigin = RelativePoint.TopLeft;
        content.RenderTransform = new MatrixTransform(new Matrix(zoom, 0, 0, zoom, -CameraX * zoom, -CameraY * zoom));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsActionControl(e.Source)) return;
        Focus();
        _pressPoint = e.GetPosition(this);
        _mousePressed = true;
        _mouseDragging = false;
        _pressedTile = FindTile(e.Source);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_mousePressed || DataContext is not DiscoverViewModel vm) return;
        var current = e.GetPosition(this);
        var delta = current - _pressPoint;
        if (!_mouseDragging && Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) < 6) return;
        if (!_mouseDragging) _mouseDragging = true;
        vm.PanBy(delta.X, delta.Y);
        _pressPoint = current;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_mousePressed) return;
        var wasDragging = _mouseDragging;
        var tile = _pressedTile;
        _mousePressed = false;
        if (DataContext is DiscoverViewModel vm) vm.CommitCamera();
        if (!wasDragging && DataContext is DiscoverViewModel discover) discover.ActivateTileFromPointer(tile);
        e.Pointer.Capture(null);
        e.Handled = true;
        _mouseDragging = false;
        _pressedTile = null;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _mousePressed = false;
        _mouseDragging = false;
        _pressedTile = null;
    }

    private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
    {
        if (_mousePressed || DataContext is not DiscoverViewModel vm) return;
        vm.PanBy(-e.Delta.X, -e.Delta.Y);
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
}
