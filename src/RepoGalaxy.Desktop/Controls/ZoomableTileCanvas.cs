using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Desktop.ViewModels;
using System.Diagnostics;
using System.Globalization;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// A low-allocation, self-drawn tile world. Camera gestures only update the camera
/// matrix; repository actions are hit-tested before the drag/peek state machine.
/// </summary>
public class ZoomableTileCanvas : Control
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
    private TileActionKind _pressedAction;
    private MetroTileViewModel? _actionTile;
    private TileActionKind _hoveredAction;
    private MetroTileViewModel? _hoveredActionTile;
    private readonly ICameraFrameScheduler _frameScheduler;
    private readonly CameraInputAccumulator _input = new();
    private readonly Dictionary<(int ContentId, byte Part, int ValueHash, int MaxLength, int Size, uint Color), FormattedText> _textCache = [];
    private readonly Queue<(int ContentId, byte Part, int ValueHash, int MaxLength, int Size, uint Color)> _textLru = new();
    private readonly Dictionary<(uint Accent, bool Dark), SkeletonStyle> _skeletonStyles = [];
    private readonly DispatcherTimer _cameraIdleTimer;

    private static readonly Geometry LikeIcon =
        Geometry.Parse("M3,9 L7,9 L9,4 C9,2 12,2 12,5 L12,7 L17,7 C18,7 18,8 18,9 L16,16 C16,17 15,18 14,18 L7,18 L7,10 L3,10 Z");
    private static readonly Geometry DislikeIcon =
        Geometry.Parse("M3,11 L7,11 L9,16 C9,18 12,18 12,15 L12,13 L17,13 C18,13 18,12 18,11 L16,4 C16,3 15,2 14,2 L7,2 L7,10 L3,10 Z");
    private static readonly Geometry BookmarkIcon =
        Geometry.Parse("M5,3 L15,3 L15,17 L10,13 L5,17 Z");
    private static readonly Geometry StarIcon =
        Geometry.Parse("M10,2 L12.5,7.2 L18,8 L14,12 L15,18 L10,15 L5,18 L6,12 L2,8 L7.5,7.2 Z");

    public double CameraX { get => GetValue(CameraXProperty); set => SetValue(CameraXProperty, value); }
    public double CameraY { get => GetValue(CameraYProperty); set => SetValue(CameraYProperty, value); }
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public int SkeletonRevision { get => GetValue(SkeletonRevisionProperty); set => SetValue(SkeletonRevisionProperty, value); }

    public ZoomableTileCanvas()
    {
        _frameScheduler = new AvaloniaCameraFrameScheduler(this);
        _cameraIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _cameraIdleTimer.Tick += (_, _) =>
        {
            _cameraIdleTimer.Stop();
            if (DataContext is DiscoverViewModel vm) _ = vm.SaveCameraAsync();
        };
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
        if (change.Property == CameraXProperty
            || change.Property == CameraYProperty
            || change.Property == ZoomProperty
            || change.Property == SkeletonRevisionProperty)
            InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DataContext is not DiscoverViewModel vm) return;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var viewport = new Rect(Bounds.Size);

        foreach (var slot in vm.RenderedSkeletonSlots)
        {
            var left = (slot.Column * 100d - CameraX) * Zoom;
            var top = (slot.Row * 100d - CameraY) * Zoom;
            var width = (slot.ColumnSpan * 96d + (slot.ColumnSpan - 1) * 4d) * Zoom;
            var height = (slot.RowSpan * 96d + (slot.RowSpan - 1) * 4d) * Zoom;
            var bounds = new Rect(left, top, width, height);
            if (!viewport.Intersects(bounds)) continue;

            var accent = SkeletonColor(slot.Content.AccentKey);
            var style = GetSkeletonStyle(accent, ActualThemeVariant == ThemeVariant.Dark);
            context.DrawRectangle(style.Fill, null, bounds);
            if (width < 54 || height < 40) continue;
            var inset = Math.Max(6, 10 * Zoom);
            var titleSize = Math.Clamp((int)Math.Round(11 * Zoom), 9, 16);
            var categorySize = Math.Clamp((int)Math.Round(8 * Zoom), 7, 11);
            var contentId = StringComparer.Ordinal.GetHashCode(slot.Content.Key);
            var title = GetText(contentId, 1, slot.Content.Title, width >= 180 ? 32 : 16, titleSize, style.Foreground);
            var category = GetText(contentId, 2, slot.Content.Caption, 14, categorySize, style.Secondary);
            using (context.PushClip(bounds.Deflate(inset)))
            {
                context.DrawText(category, new Point(left + inset, top + inset));
                if (height >= 66)
                    context.DrawText(title, new Point(left + inset, top + inset + Math.Max(14, 17 * Zoom)));
                if (height >= 132)
                {
                    var bodySize = Math.Clamp((int)Math.Round(9 * Zoom), 8, 13);
                    var body = GetText(contentId, 3, slot.Content.Subtitle, width >= 180 ? 54 : 28, bodySize, style.Secondary);
                    context.DrawText(body, new Point(left + inset, top + height - inset - Math.Max(14, 17 * Zoom)));
                }
            }
        }

        RenderRealTiles(context, viewport, vm);
        TilePerformanceMetrics.Allocation(
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
            GC.CollectionCount(0) - gen0,
            GC.CollectionCount(1) - gen1,
            GC.CollectionCount(2) - gen2);
    }

    private void RenderRealTiles(DrawingContext context, Rect viewport, DiscoverViewModel vm)
    {
        var tiles = vm.Tiles;
        var visibleCount = 0;
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            var bounds = Project(tile);
            if (!viewport.Intersects(bounds)) continue;
            visibleCount++;
            var focusOpacity = ReferenceEquals(tile, vm.FocusedTile)
                ? 1
                : Math.Clamp(1 - vm.DetailProgress * .78, .22, 1);
            using (context.PushOpacity(tile.DisplayOpacity * focusOpacity))
            {
                if (tile.IsRepository && tile.RepositoryLayout is not null)
                    RenderRepositoryTile(context, tile, bounds);
                else
                    RenderInformationalTile(context, tile, bounds);
            }
        }
        TilePerformanceMetrics.MaterializedControlCount(visibleCount);
    }

    private void RenderInformationalTile(DrawingContext context, MetroTileViewModel tile, Rect bounds)
    {
        using (context.PushOpacity(tile.BackgroundLayerOpacity))
            context.DrawRectangle(tile.Background, null, bounds);

        var inset = Math.Max(6, 10 * Zoom);
        var clip = bounds.Deflate(inset);
        if (clip.Width <= 1 || clip.Height <= 1) return;
        using (context.PushClip(clip))
        {
            var kind = GetText(tile.RenderId, 4, tile.KindLabel, 24,
                Math.Clamp((int)Math.Round(8 * Zoom), 7, 11), BrushColor(tile.SecondaryForeground));
            context.DrawText(kind, new Point(bounds.X + inset, bounds.Y + inset));
            if (bounds.Height >= 58)
            {
                var title = GetText(tile.RenderId, 1, tile.Title, bounds.Width > 360 ? 54 : 28,
                    Math.Clamp((int)Math.Round(15 * Zoom), 10, 22), BrushColor(tile.Foreground));
                context.DrawText(title, new Point(bounds.X + inset, bounds.Y + Math.Max(28, bounds.Height * .42)));
            }
            if (bounds.Height >= 88)
            {
                var caption = GetText(tile.RenderId, 2, tile.Caption, bounds.Width > 360 ? 62 : 30,
                    Math.Clamp((int)Math.Round(9 * Zoom), 8, 13), BrushColor(tile.SecondaryForeground));
                context.DrawText(caption, new Point(bounds.X + inset, bounds.Bottom - inset - Math.Max(12, 14 * Zoom)));
            }
        }
    }

    private void RenderRepositoryTile(DrawingContext context, MetroTileViewModel tile, Rect bounds)
    {
        var layout = tile.RepositoryLayout!;
        using (context.PushOpacity(tile.BackgroundLayerOpacity))
            context.DrawRectangle(tile.Background, null, bounds);

        var cover = ProjectRelative(tile, layout.Cover);
        if (layout.UsesCover && cover.Width > 1 && cover.Height > 1)
        {
            if (tile.BackgroundImage is { } image)
                DrawImageUniformToFill(context, image, cover);
            else
                DrawFallbackCover(context, tile, cover);
        }

        if (tile.IsFeatured)
        {
            var scrim = new Rect(bounds.X, bounds.Y + bounds.Height * .38, bounds.Width, bounds.Height * .62);
            using (context.PushOpacity(.72))
                context.DrawRectangle(tile.Scrim, null, scrim);
        }

        var textArea = ProjectRelative(tile, layout.Text);
        if (layout.UsesWideLayout)
            RenderWideRepositoryText(context, tile, bounds, textArea, layout);
        else
            RenderFeaturedRepositoryText(context, tile, bounds, textArea);

        for (var index = 0; index < layout.Actions.Count; index++)
            DrawTileAction(context, tile, layout.Actions[index]);
    }

    private void RenderWideRepositoryText(
        DrawingContext context,
        MetroTileViewModel tile,
        Rect bounds,
        Rect textArea,
        RepositoryTileLayout layout)
    {
        if (textArea.Width <= 4 || textArea.Height <= 4) return;
        var titleSize = Math.Clamp((int)Math.Round(15 * Zoom), 10, 23);
        var bodySize = Math.Clamp((int)Math.Round(9 * Zoom), 8, 14);
        var captionSize = Math.Clamp((int)Math.Round(8 * Zoom), 7, 12);
        var topClip = new Rect(textArea.X, textArea.Y, textArea.Width, Math.Max(1, 56 * Zoom));
        using (context.PushClip(topClip))
        {
            var title = GetText(tile.RenderId, 1, tile.Title, 56, titleSize, BrushColor(tile.Foreground));
            context.DrawText(title, new Point(textArea.X, textArea.Y));
            var firstY = textArea.Y + Math.Max(20, 23 * Zoom);
            if (!string.IsNullOrEmpty(tile.SubtitleLine1))
            {
                var first = GetText(tile.RenderId, 7, tile.SubtitleLine1, 62, bodySize, BrushColor(tile.SecondaryForeground));
                context.DrawText(first, new Point(textArea.X, firstY));
            }
            if (!string.IsNullOrEmpty(tile.SubtitleLine2))
            {
                var second = GetText(tile.RenderId, 8, tile.SubtitleLine2, 62, bodySize, BrushColor(tile.SecondaryForeground));
                context.DrawText(second, new Point(textArea.X, firstY + Math.Max(12, 14 * Zoom)));
            }
        }

        var firstAction = ProjectRelative(tile, layout.Actions[0].Bounds);
        var captionWidth = Math.Max(1, firstAction.Left - textArea.X - 8 * Zoom);
        if (captionWidth > 24)
        {
            using (context.PushClip(new Rect(textArea.X, bounds.Bottom - 30 * Zoom, captionWidth, 24 * Zoom)))
            {
                var caption = GetText(tile.RenderId, 2, tile.Caption, 48, captionSize, BrushColor(tile.SecondaryForeground));
                context.DrawText(caption, new Point(textArea.X, bounds.Bottom - 24 * Zoom));
            }
        }
    }

    private void RenderFeaturedRepositoryText(DrawingContext context, MetroTileViewModel tile, Rect bounds, Rect textArea)
    {
        var actionTop = bounds.Bottom - 36 * Zoom;
        var textTop = Math.Max(textArea.Top, bounds.Y + bounds.Height * .42);
        var textBottom = Math.Min(textArea.Bottom, actionTop - 3 * Zoom);
        if (textArea.Width <= 4 || textBottom <= textTop) return;
        using (context.PushClip(new Rect(textArea.X, textTop, textArea.Width, textBottom - textTop)))
        {
            var kind = GetText(tile.RenderId, 4, tile.KindLabel, 20,
                Math.Clamp((int)Math.Round(8 * Zoom), 7, 11), BrushColor(tile.SecondaryForeground));
            context.DrawText(kind, new Point(textArea.X, textTop));
            var titleY = textTop + Math.Max(14, 15 * Zoom);
            var title = GetText(tile.RenderId, 1, tile.Title, 28,
                Math.Clamp((int)Math.Round(14 * Zoom), 10, 23), BrushColor(tile.Foreground));
            context.DrawText(title, new Point(textArea.X, titleY));
            if (!string.IsNullOrEmpty(tile.SubtitleLine1))
            {
                var subtitle = GetText(tile.RenderId, 7, tile.SubtitleLine1, 30,
                    Math.Clamp((int)Math.Round(9 * Zoom), 8, 14), BrushColor(tile.SecondaryForeground));
                context.DrawText(subtitle, new Point(textArea.X, titleY + Math.Max(17, 20 * Zoom)));
            }
        }
    }

    private void DrawTileAction(DrawingContext context, MetroTileViewModel tile, TileActionHitRegion region)
    {
        var bounds = ProjectRelative(tile, region.Bounds);
        if (bounds.Width < 12 || bounds.Height < 12) return;
        var selected = region.Action switch
        {
            TileActionKind.Like => tile.IsLiked,
            TileActionKind.Bookmark => tile.RepositoryItem?.Item.Repository.IsBookmarked == true,
            TileActionKind.GitHubStar => tile.IsStarred,
            _ => false
        };
        var hovered = ReferenceEquals(_hoveredActionTile, tile) && _hoveredAction == region.Action;
        var pressed = ReferenceEquals(_actionTile, tile) && _pressedAction == region.Action;
        if (selected || hovered || pressed)
        {
            using (context.PushOpacity(pressed ? .78 : selected ? .62 : .42))
                context.DrawRectangle(tile.Scrim, null, bounds);
        }

        var geometry = region.Action switch
        {
            TileActionKind.Like => LikeIcon,
            TileActionKind.Bookmark => BookmarkIcon,
            TileActionKind.GitHubStar => StarIcon,
            TileActionKind.Dislike => DislikeIcon,
            _ => null
        };
        if (geometry is null) return;
        var iconSize = Math.Min(16 * Zoom, bounds.Height - 8 * Zoom);
        var iconLeft = bounds.X + 6 * Zoom;
        var iconTop = bounds.Y + (bounds.Height - iconSize) / 2;
        var transform = Matrix.CreateScale(iconSize / 20d, iconSize / 20d)
            * Matrix.CreateTranslation(iconLeft, iconTop);
        using (context.PushTransform(transform))
            context.DrawGeometry(tile.Foreground, null, geometry);

        if (region.Action == TileActionKind.GitHubStar && bounds.Width >= 48)
        {
            var count = GetText(tile.RenderId, 9, tile.StarCountText, 9,
                Math.Clamp((int)Math.Round(9 * Zoom), 8, 12), BrushColor(tile.Foreground));
            context.DrawText(count, new Point(bounds.X + 26 * Zoom, bounds.Y + Math.Max(5, 7 * Zoom)));
        }
    }

    private static void DrawImageUniformToFill(DrawingContext context, Bitmap image, Rect destination)
    {
        var sourceSize = image.Size;
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0 || destination.Width <= 0 || destination.Height <= 0) return;
        var sourceAspect = sourceSize.Width / sourceSize.Height;
        var destinationAspect = destination.Width / destination.Height;
        Rect source;
        if (sourceAspect > destinationAspect)
        {
            var width = sourceSize.Height * destinationAspect;
            source = new Rect((sourceSize.Width - width) / 2, 0, width, sourceSize.Height);
        }
        else
        {
            var height = sourceSize.Width / destinationAspect;
            source = new Rect(0, (sourceSize.Height - height) / 2, sourceSize.Width, height);
        }
        context.DrawImage(image, source, destination);
    }

    private void DrawFallbackCover(DrawingContext context, MetroTileViewModel tile, Rect destination)
    {
        if (tile.IconGeometry is null) return;
        var size = Math.Min(destination.Width, destination.Height) * .62;
        var transform = Matrix.CreateScale(size / 96d, size / 96d)
            * Matrix.CreateTranslation(destination.X + (destination.Width - size) / 2,
                destination.Y + (destination.Height - size) / 2);
        using (context.PushOpacity(.28))
        using (context.PushTransform(transform))
            context.DrawGeometry(tile.Foreground, null, tile.IconGeometry);
    }

    private Rect Project(MetroTileViewModel tile) => new(
        (tile.Left - CameraX) * Zoom,
        (tile.Top - CameraY) * Zoom,
        tile.Width * Zoom,
        tile.Height * Zoom);

    private Rect ProjectRelative(MetroTileViewModel tile, TileWorldRect relative) => new(
        (tile.Left + relative.X - CameraX) * Zoom,
        (tile.Top + relative.Y - CameraY) * Zoom,
        relative.Width * Zoom,
        relative.Height * Zoom);

    public MetroTileViewModel? GetTileAt(Point point)
    {
        if (DataContext is not DiscoverViewModel vm) return null;
        for (var index = vm.Tiles.Count - 1; index >= 0; index--)
        {
            var tile = vm.Tiles[index];
            if (Project(tile).Contains(point)) return tile;
        }
        return null;
    }

    private bool TryHitAction(Point point, out MetroTileViewModel? tile, out TileActionKind action)
    {
        tile = GetTileAt(point);
        action = TileActionKind.None;
        if (tile?.RepositoryLayout is not { } layout) return false;
        for (var index = 0; index < layout.Actions.Count; index++)
        {
            var region = layout.Actions[index];
            if (!ProjectRelative(tile, region.Bounds).Contains(point)) continue;
            action = region.Action;
            return true;
        }
        tile = null;
        return false;
    }

    private static Color BrushColor(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color : Colors.White;

    private SkeletonStyle GetSkeletonStyle(Color accent, bool dark)
    {
        var key = (accent.ToUInt32(), dark);
        if (_skeletonStyles.TryGetValue(key, out var style)) return style;
        var surface = dark ? Color.FromRgb(17, 24, 33) : Color.FromRgb(246, 248, 250);
        var fill = Blend(surface, accent, dark ? .28 : .14);
        var foreground = dark ? Color.FromRgb(245, 248, 252) : Color.FromRgb(25, 33, 43);
        var secondary = dark ? Color.FromRgb(211, 220, 232) : Color.FromRgb(67, 78, 91);
        style = new(new SolidColorBrush(WithAlpha(fill, 222)), foreground, secondary);
        _skeletonStyles[key] = style;
        return style;
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
        if (Math.Abs(input.ZoomFactor - 1) > .0001)
            vm.ZoomByFactor(input.ZoomFactor, input.Anchor.X, input.Anchor.Y, tile);
        _cameraIdleTimer.Stop();
        _cameraIdleTimer.Start();
        TilePerformanceMetrics.FrameCommitted(stopwatch.Elapsed.TotalMilliseconds);
    });

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            || IsActionControl(e.Source))
            return;
        Focus();
        _pressPoint = e.GetPosition(this);
        if (TryHitAction(_pressPoint, out var actionTile, out var action))
        {
            _pressedAction = action;
            _actionTile = actionTile;
            _hoveredAction = action;
            _hoveredActionTile = actionTile;
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        _mousePressed = true;
        _mouseDragging = false;
        _pressedTile = GetTileAt(_pressPoint);
        if (DataContext is DiscoverViewModel vm) vm.BeginTilePointerInteraction(_pressedTile);
        InvalidateVisual();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var current = e.GetPosition(this);
        if (_pressedAction != TileActionKind.None)
        {
            UpdateHoveredAction(current);
            e.Handled = true;
            return;
        }
        if (!_mousePressed || DataContext is not DiscoverViewModel vm)
        {
            UpdateHoveredAction(current);
            return;
        }

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

    private void UpdateHoveredAction(Point point)
    {
        TryHitAction(point, out var tile, out var action);
        if (ReferenceEquals(tile, _hoveredActionTile) && action == _hoveredAction) return;
        _hoveredActionTile = tile;
        _hoveredAction = action;
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressedAction != TileActionKind.None)
        {
            var pressedAction = _pressedAction;
            var pressedTile = _actionTile;
            var point = e.GetPosition(this);
            var isSameTarget = TryHitAction(point, out var releasedTile, out var releasedAction)
                && ReferenceEquals(pressedTile, releasedTile)
                && pressedAction == releasedAction;
            ClearActionPress();
            e.Pointer.Capture(null);
            if (isSameTarget && pressedTile is not null && DataContext is DiscoverViewModel vm)
                ExecuteTileAction(vm, pressedTile, pressedAction);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!_mousePressed) return;
        var wasDragging = _mouseDragging;
        var tile = _pressedTile;
        _mousePressed = false;
        if (DataContext is DiscoverViewModel discover)
        {
            discover.EndTilePointerInteraction();
            discover.CommitCamera();
            if (!wasDragging && tile is not null) discover.ActivateTileFromPointer(tile);
        }
        e.Pointer.Capture(null);
        e.Handled = true;
        _mouseDragging = false;
        _pressedTile = null;
        InvalidateVisual();
    }

    private static void ExecuteTileAction(DiscoverViewModel vm, MetroTileViewModel tile, TileActionKind action)
    {
        switch (action)
        {
            case TileActionKind.Like:
                vm.LikeTileFromPointer(tile);
                break;
            case TileActionKind.Bookmark:
                vm.SaveTileFromPointer(tile);
                break;
            case TileActionKind.GitHubStar:
                vm.StarTileFromPointer(tile);
                break;
            case TileActionKind.Dislike:
                vm.DislikeTileFromPointer(tile);
                break;
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (DataContext is DiscoverViewModel vm) vm.EndTilePointerInteraction();
        _mousePressed = false;
        _mouseDragging = false;
        _pressedTile = null;
        ClearActionPress();
        InvalidateVisual();
    }

    private void ClearActionPress()
    {
        _pressedAction = TileActionKind.None;
        _actionTile = null;
    }

    private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
    {
        if (_mousePressed || _pressedAction != TileActionKind.None || DataContext is not DiscoverViewModel) return;
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
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.Add or Key.Subtract or Key.OemPlus or Key.OemMinus or Key.Home or Key.Escape)
            e.Handled = true;
    }

    private static bool IsActionControl(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control is Button button && !button.Classes.Contains("tile-hit")
                || control is TextBox or ComboBox or ToggleSwitch)
                return true;
        return false;
    }

    private FormattedText GetText(int contentId, byte part, string value, int maximumLength, int size, Color color)
    {
        var cacheKey = (contentId, part, StringComparer.Ordinal.GetHashCode(value), maximumLength, size, color.ToUInt32());
        if (_textCache.TryGetValue(cacheKey, out var text)) return text;
        while (_textCache.Count >= 768 && _textLru.TryDequeue(out var oldest))
            _textCache.Remove(oldest);
        text = new FormattedText(
            Truncate(value, maximumLength),
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI, Microsoft YaHei UI"),
            size,
            new SolidColorBrush(color));
        _textCache[cacheKey] = text;
        _textLru.Enqueue(cacheKey);
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
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        const double m = .16;
        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    private static Color Blend(Color baseColor, Color accent, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(baseColor.R + (accent.R - baseColor.R) * amount),
            (byte)Math.Round(baseColor.G + (accent.G - baseColor.G) * amount),
            (byte)Math.Round(baseColor.B + (accent.B - baseColor.B) * amount));
    }

    private static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Clamp(Math.Round(alpha), 0, 255), color.R, color.G, color.B);

    private static string Truncate(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.ReplaceLineEndings(" ").Trim();
        return value.Length <= maximumLength ? value : value[..Math.Max(1, maximumLength - 1)] + "…";
    }

    private sealed record SkeletonStyle(IBrush Fill, Color Foreground, Color Secondary);
}
