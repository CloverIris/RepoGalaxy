using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Views;

public partial class DiscoverView : UserControl
{
    private ScrollViewer? _scrollViewer;
    private Point _pressPoint;
    private Vector _pressOffset;
    private bool _pressed;
    private bool _dragging;

    public DiscoverView()
    {
        AvaloniaXamlLoader.Load(this);
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _scrollViewer = this.FindControl<ScrollViewer>("TileScrollViewer");
        Dispatcher.UIThread.Post(async () =>
        {
            if (DataContext is not DiscoverViewModel vm || _scrollViewer is null) return;
            await vm.EnsureTileExtentAsync(_scrollViewer.Viewport.Width, _scrollViewer.Viewport.Height);
            _scrollViewer.Offset = new Vector(vm.TileViewportX, vm.TileViewportY);
            UpdateOpacity(vm);
        }, DispatcherPriority.Loaded);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_scrollViewer is null || DataContext is not DiscoverViewModel vm) return;
        _ = vm.EnsureTileExtentAsync(_scrollViewer.Viewport.Width, _scrollViewer.Viewport.Height);
        UpdateOpacity(vm);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_scrollViewer is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsInteractive(e.Source)) return;
        _pressPoint = e.GetPosition(this); _pressOffset = _scrollViewer.Offset; _pressed = true; _dragging = false;
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressed || _scrollViewer is null) return;
        var delta = e.GetPosition(this) - _pressPoint;
        if (!_dragging && Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) < 6) return;
        _dragging = true;
        _scrollViewer.Offset = new Vector(Math.Max(0, _pressOffset.X - delta.X), Math.Max(0, _pressOffset.Y - delta.Y));
        if (DataContext is DiscoverViewModel vm) UpdateOpacity(vm);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false; e.Pointer.Capture(null);
        if (_scrollViewer is not null && DataContext is DiscoverViewModel vm) _ = vm.SaveTileViewportAsync(_scrollViewer.Offset.X, _scrollViewer.Offset.Y);
        if (_dragging) e.Handled = true;
        _dragging = false;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_scrollViewer is null) return;
        var horizontal = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var delta = e.Delta.Y * 72;
        _scrollViewer.Offset = horizontal
            ? new Vector(Math.Max(0, _scrollViewer.Offset.X - delta), _scrollViewer.Offset.Y)
            : new Vector(_scrollViewer.Offset.X, Math.Max(0, _scrollViewer.Offset.Y - delta));
        if (DataContext is DiscoverViewModel vm) { UpdateOpacity(vm); _ = vm.SaveTileViewportAsync(_scrollViewer.Offset.X, _scrollViewer.Offset.Y); }
        e.Handled = true;
    }

    private void UpdateOpacity(DiscoverViewModel vm)
    {
        if (_scrollViewer is null) return;
        vm.UpdateTileEdgeOpacity(_scrollViewer.Offset.X, _scrollViewer.Offset.Y, _scrollViewer.Viewport.Width, _scrollViewer.Viewport.Height);
    }

    private static bool IsInteractive(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control is Button or TextBox or ComboBox) return true;
        return false;
    }
}
