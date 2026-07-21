using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.Views;

public partial class DiscoverView : UserControl
{
    private Control? _worldHost;
    private double _lastPinchScale = 1;

    public DiscoverView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _worldHost = this.FindControl<Control>("WorldHost");
        if (_worldHost is not null && !_worldHost.GestureRecognizers.OfType<PinchGestureRecognizer>().Any())
        {
            _worldHost.GestureRecognizers.Add(new PinchGestureRecognizer());
            _worldHost.AddHandler(InputElement.PinchEvent, OnPinch);
            _worldHost.AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);
            _worldHost.AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent, OnTouchPadMagnify);
        }
        Dispatcher.UIThread.Post(UpdateViewport, DispatcherPriority.Loaded);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateViewport();

    private void UpdateViewport()
    {
        if (_worldHost is null || DataContext is not DiscoverViewModel vm || _worldHost.Bounds.Width <= 0 || _worldHost.Bounds.Height <= 0) return;
        _ = vm.SetViewportAsync(_worldHost.Bounds.Width, _worldHost.Bounds.Height);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_worldHost is null || DataContext is not DiscoverViewModel vm || !IsInsideWorld(e.Source)) return;
        var tileWorld = this.FindControl<Controls.ZoomableTileCanvas>("TileWorld");
        if (tileWorld is null) return;
        var point = e.GetPosition(_worldHost);
        if (TileInputClassifier.Classify(e.Delta.X, e.Delta.Y) == TileWheelIntent.Pan)
            tileWorld.QueuePan(-e.Delta.X * 72, -e.Delta.Y * 72);
        else
            tileWorld.QueueZoom(e.Delta.Y, point.X, point.Y, FindTile(e.Source));
        e.Handled = true;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (DataContext is not DiscoverViewModel) return;
        var tileWorld = this.FindControl<Controls.ZoomableTileCanvas>("TileWorld");
        if (tileWorld is null) return;
        var incremental = e.Scale / Math.Max(.001, _lastPinchScale);
        _lastPinchScale = e.Scale;
        tileWorld.QueueZoomFactor(incremental, e.ScaleOrigin.X, e.ScaleOrigin.Y, FindTile(e.Source));
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        _lastPinchScale = 1;
        if (DataContext is DiscoverViewModel vm) vm.CommitCamera();
    }

    private void OnTouchPadMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (_worldHost is null || DataContext is not DiscoverViewModel) return;
        var tileWorld = this.FindControl<Controls.ZoomableTileCanvas>("TileWorld");
        if (tileWorld is null) return;
        var point = e.GetPosition(_worldHost);
        tileWorld.QueueZoomFactor(Math.Exp(Math.Clamp(e.Delta.Y, -.35, .35)), point.X, point.Y, FindTile(e.Source));
        e.Handled = true;
    }

    private bool IsInsideWorld(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (ReferenceEquals(control, _worldHost)) return true;
        return false;
    }

    private static MetroTileViewModel? FindTile(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control.DataContext is MetroTileViewModel tile) return tile;
        return null;
    }
}
