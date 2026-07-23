using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.Media;
using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>A fixed-pixel, pannable viewport for the far semantic mosaic.</summary>
public sealed class SemanticMosaicViewport : ContentControl
{
    public static readonly StyledProperty<double> OffsetXProperty =
        AvaloniaProperty.Register<SemanticMosaicViewport, double>(nameof(OffsetX), 24);
    public static readonly StyledProperty<double> OffsetYProperty =
        AvaloniaProperty.Register<SemanticMosaicViewport, double>(nameof(OffsetY), 24);

    private Point _pressPoint;
    private bool _pressed;
    private bool _dragging;
    private SemanticMosaicItemViewModel? _pressedItem;

    public double OffsetX { get => GetValue(OffsetXProperty); set => SetValue(OffsetXProperty, value); }
    public double OffsetY { get => GetValue(OffsetYProperty); set => SetValue(OffsetYProperty, value); }

    public SemanticMosaicViewport()
    {
        Focusable = true;
        ClipToBounds = true;
        GestureRecognizers.Add(new ScrollGestureRecognizer { CanHorizontallyScroll = true, CanVerticallyScroll = false, IsScrollInertiaEnabled = true });
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.ScrollGestureEvent, OnScrollGesture);
        AddHandler(InputElement.ScrollGestureEndedEvent, OnScrollGestureEnded);
        SizeChanged += (_, _) => Normalize();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OffsetXProperty || change.Property == OffsetYProperty) ApplyOffset();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ApplyOffset();
        Normalize();
    }

    private void ApplyOffset()
    {
        if (Content is not Visual content) return;
        content.RenderTransformOrigin = RelativePoint.TopLeft;
        content.RenderTransform = new TranslateTransform(OffsetX, OffsetY);
    }

    private void Normalize()
    {
        if (DataContext is DiscoverViewModel vm && Bounds.Width > 0 && Bounds.Height > 0)
            vm.NormalizeSemanticViewport(Bounds.Width, Bounds.Height);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Touch and pen pointers do not consistently report the mouse-left flag.
        // Restrict that check to an actual mouse so every direct-manipulation
        // pointer uses the same drag/click arbitration state machine.
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        _pressPoint = e.GetPosition(this);
        _pressed = true;
        _dragging = false;
        _pressedItem = FindItem(e.Source);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressed || DataContext is not DiscoverViewModel vm) return;
        var current = e.GetPosition(this);
        var delta = current - _pressPoint;
        if (!_dragging && Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) < 6) return;
        _dragging = true;
        vm.PanSemanticBy(delta.X, 0, Bounds.Width, Bounds.Height);
        _pressPoint = current;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed) return;
        var activate = !_dragging ? _pressedItem : null;
        ResetPointer();
        e.Pointer.Capture(null);
        if (DataContext is DiscoverViewModel vm)
        {
            vm.CommitSemanticViewport();
            vm.ActivateSemanticIndexFromPointer(activate);
        }
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => ResetPointer();

    private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
    {
        if (_pressed || DataContext is not DiscoverViewModel vm) return;
        vm.PanSemanticBy(-e.Delta.X, 0, Bounds.Width, Bounds.Height);
        e.Handled = true;
    }

    private void OnScrollGestureEnded(object? sender, ScrollGestureEndedEventArgs e)
    {
        if (DataContext is DiscoverViewModel vm) vm.CommitSemanticViewport();
    }

    private void ResetPointer() { _pressed = false; _dragging = false; _pressedItem = null; }

    private static SemanticMosaicItemViewModel? FindItem(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control.DataContext is SemanticMosaicItemViewModel item) return item;
        return null;
    }
}
