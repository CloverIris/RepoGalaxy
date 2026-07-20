using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class DiscoverViewModel
{
    private double _viewportWidth = 1200;
    private double _viewportHeight = 760;
    private CancellationTokenSource? _cameraSaveDebounce;
    private CancellationTokenSource? _semanticViewportSaveDebounce;
    private CancellationTokenSource? _detailLoadCancellation;
    private CancellationTokenSource? _cameraAnimation;
    private CancellationTokenSource? _portalAnimation;
    private CancellationTokenSource? _imageLoadCancellation;
    private string _scheduledDetailKey = string.Empty;
    private CameraState? _detailEntryCamera;
    private long _markdownImageBudget;
    private double _latchedDetailFitScale;

    public ObservableCollection<SemanticIndexItemViewModel> SemanticIndexItems { get; } = [];
    public ObservableCollection<SemanticMosaicItemViewModel> SemanticMosaicItems { get; } = [];
    public ObservableCollection<MarkdownBlockViewModel> MarkdownBlocks { get; } = [];
    public ObservableCollection<LocalIdeViewModel> AvailableIdes { get; } = [];
    public IReadOnlyList<CloneModeOption> CloneModeOptions { get; } =
    [
        new(CloneMode.Full, "完整克隆", "保留完整提交历史"),
        new(CloneMode.Shallow, "浅克隆", "仅获取当前分支的最新版本")
    ];

    [ObservableProperty] private MetroTileViewModel? _focusedTile;
    [ObservableProperty] private DetailSnapshot? _immersiveDetail;
    [ObservableProperty] private DetailSection? _selectedDetailSection;
    [ObservableProperty] private double _semanticIndexOpacity;
    [ObservableProperty] private double _tileBoardOpacity = 1;
    [ObservableProperty] private double _detailProgress;
    [ObservableProperty] private bool _isDetailLoading;
    [ObservableProperty] private bool _isSemanticIndexInteractive;
    [ObservableProperty] private bool _isDetailInteractive;
    [ObservableProperty] private DetailPresentationState _detailPresentation = DetailPresentationState.Board;
    [ObservableProperty] private double _portalX;
    [ObservableProperty] private double _portalY;
    [ObservableProperty] private double _portalWidth;
    [ObservableProperty] private double _portalHeight;
    [ObservableProperty] private double _portalContentOpacity;
    [ObservableProperty] private double _semanticCanvasWidth = 640;
    [ObservableProperty] private double _semanticCanvasHeight = 480;
    [ObservableProperty] private double _semanticViewportX = 24;
    [ObservableProperty] private double _semanticViewportY = 24;
    [ObservableProperty] private int _markdownPageNumber = 1;
    [ObservableProperty] private int _markdownPageCount;
    [ObservableProperty] private bool _hasMarkdown;
    [ObservableProperty] private LocalIdeViewModel? _selectedIde;
    [ObservableProperty] private bool _isIdeLoading;
    [ObservableProperty] private bool _isClonePanelOpen;
    [ObservableProperty] private string _cloneParentDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RepoGalaxy");
    [ObservableProperty] private CloneModeOption _selectedCloneOption = new(CloneMode.Full, "完整克隆", "保留完整提交历史");
    [ObservableProperty] private bool _isCloning;
    [ObservableProperty] private double _cloneProgress;
    [ObservableProperty] private bool _isCloneProgressIndeterminate = true;
    [ObservableProperty] private string _workspaceStatus = string.Empty;

    public string ZoomText => $"{Zoom:P0}";
    public bool IsImmersiveDetail => DetailPresentation != DetailPresentationState.Board;
    public bool IsPortalVisible => DetailPresentation != DetailPresentationState.Board && PortalWidth > 0 && PortalHeight > 0;
    public bool ShouldSuppressRightRail => DetailPresentation is DetailPresentationState.Snapping or DetailPresentationState.Full;
    public bool HasSemanticIndex => SemanticMosaicItems.Count > 0;
    public bool CanSaveImmersive => FocusedTile?.IsRepository == true;
    public bool CanOpenImmersiveSource => _externalLinks.CanOpen(ImmersiveDetail?.SourceUrl);
    public bool CanOpenInIde => FocusedTile?.IsRepository == true && !IsIdeLoading && !IsCloning;
    public string ImmersiveSaveText => FocusedTile?.SaveText ?? "收藏";
    public string SemanticIndexEmptyText => "当前 Feed、本地仓库和订阅中还没有可建立索引的语言或技术栈。";
    public string MarkdownPageText => MarkdownPageCount == 0 ? "" : $"{MarkdownPageNumber} / {MarkdownPageCount}";
    public bool CanGoPreviousMarkdownPage => MarkdownPageNumber > 1;
    public bool CanGoNextMarkdownPage => MarkdownPageNumber < MarkdownPageCount;
    public double PortalInnerOffsetX => -PortalX;
    public double PortalInnerOffsetY => -PortalY;
    public double DetailViewportWidth => Math.Max(1, _viewportWidth);
    public double DetailViewportHeight => Math.Max(1, _viewportHeight);

    public event EventHandler? ImmersiveDetailChanged;

    public CameraState Camera => new(CameraX, CameraY, Zoom, FocusedTile?.Key ?? string.Empty,
        string.IsNullOrWhiteSpace(ActiveTileFilter) ? null : SemanticIndexItems.FirstOrDefault(x => x.Title.Equals(ActiveTileFilter, StringComparison.OrdinalIgnoreCase))?.Kind,
        ActiveTileFilter);

    public async Task SetViewportAsync(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        _viewportWidth = width;
        _viewportHeight = height;
        OnPropertyChanged(nameof(DetailViewportWidth));
        OnPropertyChanged(nameof(DetailViewportHeight));
        if (DetailPresentation == DetailPresentationState.Full) SetPortal(new(0, 0, width, height));
        await EnsureTileExtentAsync(width, height);
        UpdateCameraPresentation();
    }

    public void ZoomBy(double wheelDelta, double anchorX, double anchorY, MetroTileViewModel? pointerTile = null)
    {
        CancelCameraAnimation();
        if (pointerTile is not null && DetailPresentation is DetailPresentationState.Board or DetailPresentationState.Portal) SetFocusedTile(pointerTile);
        var desired = Zoom * Math.Exp(Math.Clamp(wheelDelta, -3, 3) * .12);
        if (wheelDelta > 0)
        {
            if (FocusedTile is null) desired = Math.Min(desired, 2.5);
            else desired = Math.Min(desired, Math.Min(8, _zoomLayout.CalculateFitScale(Rect(FocusedTile), _viewportWidth, _viewportHeight) * 1.08));
        }
        ApplyCamera(_zoomLayout.ZoomAt(Camera, desired, anchorX, anchorY, _viewportWidth, _viewportHeight, TileCanvasWidth, TileCanvasHeight));
    }

    public void ZoomByFactor(double factor, double anchorX, double anchorY, MetroTileViewModel? pointerTile = null)
    {
        CancelCameraAnimation();
        if (pointerTile is not null && DetailPresentation is DetailPresentationState.Board or DetailPresentationState.Portal) SetFocusedTile(pointerTile);
        var desired = Zoom * Math.Clamp(factor, .72, 1.38);
        if (FocusedTile is null) desired = Math.Min(desired, 2.5);
        else desired = Math.Min(desired, Math.Min(8, _zoomLayout.CalculateFitScale(Rect(FocusedTile), _viewportWidth, _viewportHeight) * 1.08));
        ApplyCamera(_zoomLayout.ZoomAt(Camera, desired, anchorX, anchorY, _viewportWidth, _viewportHeight, TileCanvasWidth, TileCanvasHeight));
    }

    public void PanBy(double screenDeltaX, double screenDeltaY)
    {
        if (DetailPresentation is DetailPresentationState.Snapping or DetailPresentationState.Full) return;
        CancelCameraAnimation();
        ApplyCamera(_zoomLayout.Pan(Camera, screenDeltaX, screenDeltaY, _viewportWidth, _viewportHeight, TileCanvasWidth, TileCanvasHeight));
    }

    public void PanSemanticBy(double screenDeltaX, double screenDeltaY, double viewportWidth, double viewportHeight)
    {
        if (!IsSemanticIndexInteractive) return;
        SemanticViewportX = ClampSemanticAxis(SemanticViewportX + screenDeltaX, viewportWidth, SemanticCanvasWidth);
        SemanticViewportY = ClampSemanticAxis(SemanticViewportY + screenDeltaY, viewportHeight, SemanticCanvasHeight);
    }

    public void NormalizeSemanticViewport(double viewportWidth, double viewportHeight)
    {
        SemanticViewportX = ClampSemanticAxis(SemanticViewportX, viewportWidth, SemanticCanvasWidth);
        SemanticViewportY = ClampSemanticAxis(SemanticViewportY, viewportHeight, SemanticCanvasHeight);
    }

    public void CommitSemanticViewport()
    {
        _semanticViewportSaveDebounce?.Cancel();
        var cancellation = _semanticViewportSaveDebounce = new CancellationTokenSource();
        _ = SaveSemanticViewportAfterDelayAsync(cancellation);
    }

    public void ActivateSemanticIndexFromPointer(SemanticMosaicItemViewModel? item)
    {
        if (item is not null) ActivateSemanticIndexCommand.Execute(item);
    }

    public void ActivateTileFromPointer(MetroTileViewModel? tile)
    {
        if (tile is null) return;
        ActivateTileCommand.Execute(tile);
    }

    public void SetCloneParentDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path)) CloneParentDirectory = path;
    }

    public void SetFocusedTile(MetroTileViewModel? tile)
    {
        if (ReferenceEquals(FocusedTile, tile)) return;
        CancelDetailLoad();
        CancelPortalAnimation();
        _scheduledDetailKey = string.Empty;
        _detailEntryCamera = null;
        _latchedDetailFitScale = 0;
        FocusedTile = tile;
        ImmersiveDetail = tile is null ? null : _detailContent.CreateBaseline(CreateDetailTarget(tile));
        SelectedDetailSection = ImmersiveDetail?.Sections.FirstOrDefault();
        IsClonePanelOpen = false;
        WorkspaceStatus = string.Empty;
        ClearMarkdown();
        OnPropertyChanged(nameof(CanSaveImmersive));
        OnPropertyChanged(nameof(CanOpenImmersiveSource));
        OnPropertyChanged(nameof(CanOpenInIde));
        OnPropertyChanged(nameof(ImmersiveSaveText));
        UpdateCameraPresentation();
    }

    public void CommitCamera() => DebounceCameraSave();

    public async Task SaveCameraAsync()
    {
        var boardId = _tileBoard?.Id ?? 0;
        var camera = Camera;
        if (boardId > 0) await _tileLayout.SaveCameraAsync(boardId, camera);
    }

    private async Task SaveViewStateAsync()
    {
        var boardId = _tileBoard?.Id ?? 0;
        if (boardId <= 0) return;
        var camera = Camera;
        var semanticViewport = new SemanticViewportState(SemanticViewportX, SemanticViewportY);
        await _tileLayout.SaveCameraAsync(boardId, camera);
        await _tileLayout.SaveSemanticViewportAsync(boardId, semanticViewport);
    }

    public async Task DeactivateAsync()
    {
        CancelDetailLoad(); CancelCameraAnimation(); CancelPortalAnimation();
        await SaveViewStateAsync();
        SetFocusedTile(null);
    }

    public void Dispose()
    {
        _cameraSaveDebounce?.Cancel(); _cameraSaveDebounce?.Dispose(); _cameraSaveDebounce = null;
        _semanticViewportSaveDebounce?.Cancel(); _semanticViewportSaveDebounce?.Dispose(); _semanticViewportSaveDebounce = null;
        CancelDetailLoad(); CancelCameraAnimation(); CancelPortalAnimation();
        _imageLoadCancellation?.Cancel(); _imageLoadCancellation?.Dispose();
    }

    [RelayCommand]
    private async Task ReturnToMiddleAsync() => await AnimateCameraAsync(_zoomLayout.ZoomAt(Camera, 1, _viewportWidth / 2, _viewportHeight / 2, _viewportWidth, _viewportHeight, TileCanvasWidth, TileCanvasHeight));

    [RelayCommand]
    private async Task ShowSemanticIndexAsync() => await AnimateCameraAsync(_zoomLayout.ZoomAt(Camera, _zoomLayout.ScaleProfile.SemanticFullyVisibleZoom, _viewportWidth / 2, _viewportHeight / 2, _viewportWidth, _viewportHeight, TileCanvasWidth, TileCanvasHeight));

    [RelayCommand]
    private async Task CloseImmersiveDetailAsync()
    {
        ChangeDetailState(DetailPresentationState.Exiting);
        var target = _detailEntryCamera ?? Camera with { Zoom = 1 };
        if (target.Zoom > 1.05) target = target with { Zoom = 1 };
        await AnimateCameraAsync(target);
        _detailEntryCamera = null;
        _latchedDetailFitScale = 0;
        ChangeDetailState(DetailPresentationState.Board);
        SetPortal(DetailPortalGeometry.Empty);
    }

    [RelayCommand]
    private void SelectDetailSection(DetailSection section)
    {
        SelectedDetailSection = section;
        if (!string.IsNullOrWhiteSpace(section.Markdown)) PrepareMarkdown(section.Markdown);
        else ClearMarkdown();
    }

    [RelayCommand]
    private void PreviousMarkdownPage() { if (MarkdownPageNumber > 1) ShowMarkdownPage(MarkdownPageNumber - 1); }

    [RelayCommand]
    private void NextMarkdownPage() { if (MarkdownPageNumber < MarkdownPageCount) ShowMarkdownPage(MarkdownPageNumber + 1); }

    [RelayCommand]
    private void OpenImmersiveSource()
    {
        if (!_externalLinks.Open(ImmersiveDetail?.SourceUrl) && ImmersiveDetail is not null) ImmersiveDetail = ImmersiveDetail with { StatusText = "无法打开该 HTTPS 链接" };
    }

    [RelayCommand]
    private async Task SaveImmersiveAsync()
    {
        if (FocusedTile?.IsRepository != true) return;
        await SaveTileAsync(FocusedTile);
        OnPropertyChanged(nameof(ImmersiveSaveText));
    }

    [RelayCommand]
    private async Task OpenInIdeAsync()
    {
        var repository = FocusedTile?.RepositoryItem?.Repository.Repository;
        if (repository is null || IsIdeLoading || IsCloning) return;
        IsIdeLoading = true; OnPropertyChanged(nameof(CanOpenInIde));
        try
        {
            if (AvailableIdes.Count == 0)
            {
                var detected = await _ideDiscovery.DiscoverAsync();
                foreach (var ide in detected) AvailableIdes.Add(new(ide));
                var preferredKey = await _idePreferences.GetPreferredIdeAsync(IdeScope(), TechnologyKey(repository));
                SelectedIde = !string.IsNullOrWhiteSpace(preferredKey) ? AvailableIdes.FirstOrDefault(x => x.Descriptor.Key == preferredKey) : null;
                SelectedIde ??= _ideDiscovery.Recommend(detected, repository.PrimaryLanguage, repository.Topics) is { } recommended
                    ? AvailableIdes.FirstOrDefault(x => x.Descriptor.Key == recommended.Key) : AvailableIdes.FirstOrDefault();
            }
            if (SelectedIde is null) { WorkspaceStatus = "没有检测到可用的本地 IDE"; return; }
            await _idePreferences.SetPreferredIdeAsync(IdeScope(), TechnologyKey(repository), SelectedIde.Descriptor.Key);
            var local = await _localResolver.ResolveAsync(repository.Id, repository.Owner, repository.Name, repository.HtmlUrl);
            if (local is not null)
            {
                var launched = await _ideLauncher.OpenAsync(SelectedIde.Descriptor, local.LocalPath);
                WorkspaceStatus = launched.Success ? $"已在 {SelectedIde.Descriptor.DisplayName} 中打开" : ExplainWorkspaceError(launched.ErrorCode);
                return;
            }
            IsClonePanelOpen = true;
            WorkspaceStatus = "仓库尚未关联到本地，请选择位置后克隆";
        }
        finally { IsIdeLoading = false; OnPropertyChanged(nameof(CanOpenInIde)); }
    }

    [RelayCommand]
    private async Task StartCloneAsync()
    {
        var repository = FocusedTile?.RepositoryItem?.Repository.Repository;
        if (repository is null || SelectedIde is null || IsCloning) return;
        await _idePreferences.SetPreferredIdeAsync(IdeScope(), TechnologyKey(repository), SelectedIde.Descriptor.Key);
        IsCloning = true; CloneProgress = 0; IsCloneProgressIndeterminate = true; OnPropertyChanged(nameof(CanOpenInIde));
        var reporter = new Progress<CloneProgress>(value =>
        {
            WorkspaceStatus = value.Message;
            IsCloneProgressIndeterminate = value.Percentage is null;
            CloneProgress = value.Percentage ?? 0;
        });
        try
        {
            var cloneUrl = $"https://github.com/{repository.Owner}/{repository.Name}.git";
            var result = await _cloneService.CloneAsync(new(repository.Id, repository.Owner, repository.Name, cloneUrl, CloneParentDirectory, SelectedCloneOption.Mode, SelectedIde.Descriptor), reporter);
            WorkspaceStatus = result.Message;
            if (result.Success)
            {
                IsClonePanelOpen = false;
                var launched = await _ideLauncher.OpenAsync(SelectedIde.Descriptor, result.LocalPath);
                if (!launched.Success) WorkspaceStatus = ExplainWorkspaceError(launched.ErrorCode);
            }
        }
        finally { IsCloning = false; OnPropertyChanged(nameof(CanOpenInIde)); }
    }

    [RelayCommand]
    private async Task ActivateSemanticIndexAsync(SemanticMosaicItemViewModel mosaic)
    {
        var item = new SemanticIndexItemViewModel(mosaic.Item, _tilePalette.Create(mosaic.Item.AccentKey));
        ActiveTileFilter = item.Title;
        ApplyTileFilter();
        var matches = Tiles.Where(x => item.ContentKeys.Contains(x.Key, StringComparer.Ordinal)).ToList();
        if (matches.Count == 0) matches = Tiles.Where(x => x.Title.Contains(item.Title, StringComparison.OrdinalIgnoreCase) || x.Language.Equals(item.Title, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count > 0)
        {
            var densest = matches.OrderByDescending(candidate => matches.Count(other => DistanceSquared(candidate, other) <= 1_000_000)).ThenBy(x => x.Top).ThenBy(x => x.Left).First();
            SetFocusedTile(densest);
            await AnimateCameraAsync(_zoomLayout.CenterOn(Camera with { ActiveIndexKind = item.Kind, ActiveIndexKey = item.Title }, Rect(densest), _viewportWidth, _viewportHeight, .9));
        }
        else CommitCamera();
    }

    public void HandleKey(string key)
    {
        switch (key)
        {
            case "Left": PanBy(72, 0); break;
            case "Right": PanBy(-72, 0); break;
            case "Up": PanBy(0, 72); break;
            case "Down": PanBy(0, -72); break;
            case "Add": case "OemPlus": ZoomBy(1, _viewportWidth / 2, _viewportHeight / 2); break;
            case "Subtract": case "OemMinus": ZoomBy(-1, _viewportWidth / 2, _viewportHeight / 2); break;
            case "Home": _ = ReturnToMiddleAsync(); break;
            case "PageUp": PreviousMarkdownPage(); break;
            case "PageDown": NextMarkdownPage(); break;
            case "Escape": if (IsImmersiveDetail) _ = CloseImmersiveDetailAsync(); else if (SemanticIndexOpacity > .5) _ = ReturnToMiddleAsync(); break;
        }
        CommitCamera();
    }

    private void ApplyCamera(CameraState camera, bool persist = true)
    {
        CameraX = camera.X; CameraY = camera.Y; Zoom = camera.Zoom;
        OnPropertyChanged(nameof(ZoomText));
        UpdateCameraPresentation();
        if (persist) DebounceCameraSave();
    }

    private async Task AnimateCameraAsync(CameraState target)
    {
        CancelCameraAnimation();
        if (!MotionPreferences.AnimationsEnabled) { ApplyCamera(target); CommitCamera(); return; }
        var cancellation = _cameraAnimation = new CancellationTokenSource();
        var start = Camera;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            while (stopwatch.Elapsed.TotalMilliseconds < 160)
            {
                await Task.Delay(16, cancellation.Token);
                var t = Smooth(stopwatch.Elapsed.TotalMilliseconds / 160);
                ApplyCamera(new(Lerp(start.X, target.X, t), Lerp(start.Y, target.Y, t), Lerp(start.Zoom, target.Zoom, t), target.FocusedContentKey, target.ActiveIndexKind, target.ActiveIndexKey), false);
            }
            ApplyCamera(target); CommitCamera();
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_cameraAnimation, cancellation)) _cameraAnimation = null; cancellation.Dispose(); }
    }

    private void UpdateCameraPresentation()
    {
        var tileRect = FocusedTile is null ? (TileWorldRect?)null : Rect(FocusedTile);
        var transition = _zoomLayout.CalculateTransition(Camera, tileRect, _viewportWidth, _viewportHeight);
        SemanticIndexOpacity = transition.SemanticIndexOpacity;
        TileBoardOpacity = transition.TileBoardOpacity;
        IsSemanticIndexInteractive = transition.SemanticIndexOpacity >= .98;
        var fitForDecision = DetailPresentation is DetailPresentationState.Snapping or DetailPresentationState.Full && _latchedDetailFitScale > 0
            ? _latchedDetailFitScale
            : transition.FitScale;
        var ratio = tileRect is null ? 0 : Zoom / Math.Max(1, fitForDecision);
        DetailProgress = Smooth(Math.Clamp((ratio - .65) / .27, 0, 1));
        PortalContentOpacity = DetailProgress;

        var viewport = new TileWorldRect(CameraX, CameraY, _viewportWidth / Math.Max(_zoomLayout.ScaleProfile.MinimumZoom, Zoom), _viewportHeight / Math.Max(_zoomLayout.ScaleProfile.MinimumZoom, Zoom));
        foreach (var tile in Tiles)
        {
            var rect = Rect(tile);
            tile.IsInRenderWindow = Intersects(rect, viewport, 400);
            var visibleWidth = Math.Max(0, Math.Min(rect.X + rect.Width, viewport.X + viewport.Width) - Math.Max(rect.X, viewport.X));
            var visibleHeight = Math.Max(0, Math.Min(rect.Y + rect.Height, viewport.Y + viewport.Height) - Math.Max(rect.Y, viewport.Y));
            var visibleRatio = rect.Width <= 0 || rect.Height <= 0 ? 0 : visibleWidth * visibleHeight / (rect.Width * rect.Height);
            tile.SetEdgeOpacity(visibleRatio >= .999 ? 1 : .3 + .7 * visibleRatio);
            tile.SetFocus(ReferenceEquals(tile, FocusedTile), DetailProgress);
        }

        if (transition.ShouldPrefetch && FocusedTile is not null) ScheduleDetailLoad(FocusedTile);
        else if (ratio < .55) CancelDetailLoad();
        UpdateDetailState(ratio);
    }

    private void UpdateDetailState(double ratio)
    {
        var decision = _detailPortal.Evaluate(DetailPresentation, FocusedTile is not null, ratio);
        if (decision.State == DetailPresentationState.Board)
        {
            _latchedDetailFitScale = 0;
            ChangeDetailState(DetailPresentationState.Board); SetPortal(DetailPortalGeometry.Empty);
            return;
        }
        if (decision.ExitFull && FocusedTile is not null)
        {
            ChangeDetailState(DetailPresentationState.Exiting);
            SetPortal(Projected(FocusedTile));
            ChangeDetailState(DetailPresentationState.Portal);
            return;
        }
        if (decision.State == DetailPresentationState.Full) { SetPortal(new(0, 0, _viewportWidth, _viewportHeight)); return; }
        if (decision.State == DetailPresentationState.Snapping && !decision.StartSnap) return;
        if (decision.StartSnap)
        {
            if (FocusedTile is not null) _latchedDetailFitScale = _zoomLayout.CalculateFitScale(Rect(FocusedTile), _viewportWidth, _viewportHeight);
            _ = SnapPortalAsync();
            return;
        }
        if (FocusedTile is null) return;
        if (_detailEntryCamera is null) _detailEntryCamera = Camera with { Zoom = Math.Min(1, Camera.Zoom) };
        ChangeDetailState(DetailPresentationState.Portal);
        SetPortal(Projected(FocusedTile));
    }

    private async Task SnapPortalAsync()
    {
        if (FocusedTile is null || DetailPresentation is DetailPresentationState.Snapping or DetailPresentationState.Full) return;
        CancelPortalAnimation();
        if (_detailEntryCamera is null) _detailEntryCamera = Camera with { Zoom = Math.Min(1, Camera.Zoom) };
        var cancellation = _portalAnimation = new CancellationTokenSource();
        ChangeDetailState(DetailPresentationState.Snapping);
        var start = Projected(FocusedTile);
        SetPortal(start);
        try
        {
            await Task.Delay(32, cancellation.Token);
            var end = new DetailPortalGeometry(0, 0, _viewportWidth, _viewportHeight);
            if (!MotionPreferences.AnimationsEnabled) SetPortal(end);
            else
            {
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed.TotalMilliseconds < 160)
                {
                    await Task.Delay(16, cancellation.Token);
                    var t = Smooth(stopwatch.Elapsed.TotalMilliseconds / 160);
                    SetPortal(new(Lerp(start.X, end.X, t), Lerp(start.Y, end.Y, t), Lerp(start.Width, end.Width, t), Lerp(start.Height, end.Height, t)));
                }
                SetPortal(end);
            }
            ChangeDetailState(DetailPresentationState.Full);
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_portalAnimation, cancellation)) _portalAnimation = null; cancellation.Dispose(); }
    }

    private void ChangeDetailState(DetailPresentationState value)
    {
        if (DetailPresentation == value) return;
        DetailPresentation = value;
        IsDetailInteractive = value == DetailPresentationState.Full;
        OnPropertyChanged(nameof(IsImmersiveDetail));
        OnPropertyChanged(nameof(IsPortalVisible));
        OnPropertyChanged(nameof(ShouldSuppressRightRail));
        ImmersiveDetailChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetPortal(DetailPortalGeometry geometry)
    {
        PortalX = geometry.X; PortalY = geometry.Y; PortalWidth = geometry.Width; PortalHeight = geometry.Height;
        OnPropertyChanged(nameof(IsPortalVisible));
        OnPropertyChanged(nameof(PortalInnerOffsetX)); OnPropertyChanged(nameof(PortalInnerOffsetY));
    }

    private DetailPortalGeometry Projected(MetroTileViewModel tile) => new((tile.Left - CameraX) * Zoom, (tile.Top - CameraY) * Zoom, tile.Width * Zoom, tile.Height * Zoom);

    private void ScheduleDetailLoad(MetroTileViewModel tile)
    {
        if (_scheduledDetailKey == tile.Key && (_detailLoadCancellation is not null || ImmersiveDetail?.State == DetailLoadState.Ready)) return;
        CancelDetailLoad();
        _scheduledDetailKey = tile.Key;
        var cancellation = _detailLoadCancellation = new CancellationTokenSource();
        var target = CreateDetailTarget(tile);
        ImmersiveDetail = _detailContent.CreateBaseline(target) with { State = DetailLoadState.Loading, StatusText = "正在安全加载结构化详情" };
        SelectedDetailSection = ImmersiveDetail.Sections.FirstOrDefault();
        _ = LoadAfterDwellAsync(target, cancellation);
    }

    private async Task LoadAfterDwellAsync(DetailTarget target, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(250, cancellation.Token);
            IsDetailLoading = true;
            var result = await _detailContent.LoadAsync(target, cancellation.Token);
            if (_scheduledDetailKey != target.ContentKey || cancellation.IsCancellationRequested) return;
            ImmersiveDetail = result;
            SelectedDetailSection = result.Sections.FirstOrDefault();
            ClearMarkdown();
            OnPropertyChanged(nameof(CanOpenImmersiveSource));
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_detailLoadCancellation, cancellation)) { IsDetailLoading = false; _detailLoadCancellation = null; } cancellation.Dispose(); }
    }

    private void PrepareMarkdown(string markdown)
    {
        var repository = FocusedTile?.RepositoryItem?.Repository.Repository;
        var baseUrl = repository is null ? string.Empty : $"https://raw.githubusercontent.com/{repository.Owner}/{repository.Name}/HEAD/";
        var document = _markdown.Parse(markdown, repository?.FullName ?? "README", PageCapacity(), baseUrl);
        _markdownDocument = document;
        MarkdownPageCount = document.Pages.Count;
        HasMarkdown = document.Blocks.Count > 0;
        ShowMarkdownPage(1);
    }

    private MarkdownDocument? _markdownDocument;
    private void ShowMarkdownPage(int page)
    {
        if (_markdownDocument is null || _markdownDocument.Pages.Count == 0) return;
        MarkdownPageNumber = Math.Clamp(page, 1, _markdownDocument.Pages.Count);
        MarkdownBlocks.Clear();
        foreach (var block in _markdownDocument.Pages[MarkdownPageNumber - 1].Blocks) MarkdownBlocks.Add(new(block));
        OnPropertyChanged(nameof(MarkdownPageText)); OnPropertyChanged(nameof(CanGoPreviousMarkdownPage)); OnPropertyChanged(nameof(CanGoNextMarkdownPage));
        _ = LoadMarkdownImagesAsync();
    }

    private async Task LoadMarkdownImagesAsync()
    {
        _imageLoadCancellation?.Cancel(); _imageLoadCancellation?.Dispose();
        var cancellation = _imageLoadCancellation = new CancellationTokenSource();
        _markdownImageBudget = 20L * 1024 * 1024;
        foreach (var block in MarkdownBlocks.Where(x => x.IsImage))
        {
            try
            {
                block.IsImageLoading = true;
                var result = await _markdownImages.GetAsync(block.Url, _markdownImageBudget, cancellation.Token);
                if (result is null) { block.ImageStatus = string.IsNullOrWhiteSpace(block.AltText) ? "图片未能安全加载" : block.AltText; continue; }
                _markdownImageBudget -= result.Bytes.LongLength;
                block.Image = new Bitmap(new MemoryStream(result.Bytes, writable: false));
                block.ImageStatus = result.IsStale ? "缓存图片" : string.Empty;
            }
            catch (OperationCanceledException) { return; }
            catch { block.ImageStatus = "图片未能安全加载"; }
            finally { block.IsImageLoading = false; }
        }
    }

    private int PageCapacity() => Math.Clamp((int)(_viewportHeight / 25), 18, 42);
    private void ClearMarkdown()
    {
        _imageLoadCancellation?.Cancel(); _markdownDocument = null; MarkdownBlocks.Clear(); MarkdownPageNumber = 1; MarkdownPageCount = 0; HasMarkdown = false;
        OnPropertyChanged(nameof(MarkdownPageText)); OnPropertyChanged(nameof(CanGoPreviousMarkdownPage)); OnPropertyChanged(nameof(CanGoNextMarkdownPage));
    }

    private void CancelDetailLoad() { _detailLoadCancellation?.Cancel(); _detailLoadCancellation = null; IsDetailLoading = false; }
    private void CancelCameraAnimation() { _cameraAnimation?.Cancel(); _cameraAnimation = null; }
    private void CancelPortalAnimation() { _portalAnimation?.Cancel(); _portalAnimation = null; }

    private void DebounceCameraSave()
    {
        _cameraSaveDebounce?.Cancel();
        var cancellation = _cameraSaveDebounce = new CancellationTokenSource();
        _ = SaveAfterDelayAsync(cancellation);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try { await Task.Delay(220, cancellation.Token); await SaveCameraAsync(); }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_cameraSaveDebounce, cancellation)) _cameraSaveDebounce = null; cancellation.Dispose(); }
    }

    private async Task SaveSemanticViewportAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(220, cancellation.Token);
            if (_tileBoard is not null) await _tileLayout.SaveSemanticViewportAsync(_tileBoard.Id, new(SemanticViewportX, SemanticViewportY), cancellation.Token);
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_semanticViewportSaveDebounce, cancellation)) _semanticViewportSaveDebounce = null; cancellation.Dispose(); }
    }

    private async Task BuildSemanticIndexAsync()
    {
        var signals = new List<SemanticIndexSignal>();
        foreach (var tile in Tiles)
        {
            if (tile.IsLanguage) signals.Add(new(SemanticIndexKind.Language, tile.Title, tile.Key, tile.Title, SemanticIndexSignalOrigin.DedicatedTile, !string.IsNullOrWhiteSpace(OfficialTechnologyLinks.Get(tile.Title))));
            if (tile.IsTechnology) signals.Add(new(SemanticIndexKind.Framework, tile.Title, tile.Key, tile.Language, SemanticIndexSignalOrigin.DedicatedTile, !string.IsNullOrWhiteSpace(OfficialTechnologyLinks.Get(tile.Title))));
            if (tile.RepositoryItem is null) continue;
            var language = tile.RepositoryItem.Repository.PrimaryLanguage;
            signals.Add(new(SemanticIndexKind.Language, language, tile.Key, language, SemanticIndexSignalOrigin.Feed, !string.IsNullOrWhiteSpace(OfficialTechnologyLinks.Get(language))));
            foreach (var topic in tile.RepositoryItem.Repository.Topics)
                signals.Add(new(SemanticIndexKind.Framework, topic, tile.Key, language, SemanticIndexSignalOrigin.Feed, !string.IsNullOrWhiteSpace(OfficialTechnologyLinks.Get(topic))));
        }
        foreach (var subscription in await _store.GetSubscriptionsAsync())
        {
            foreach (var language in subscription.Languages)
                signals.Add(new(SemanticIndexKind.Language, language, $"subscription:{subscription.Id}", language, SemanticIndexSignalOrigin.Subscription, !string.IsNullOrWhiteSpace(OfficialTechnologyLinks.Get(language))));
            foreach (var topic in subscription.Topics)
                signals.Add(new(SemanticIndexKind.Framework, topic, $"subscription:{subscription.Id}", Origin: SemanticIndexSignalOrigin.Subscription, HasOfficialLink: !string.IsNullOrWhiteSpace(OfficialTechnologyLinks.Get(topic))));
        }
        var localRepositories = await _repositories.GetLocalRepositoriesAsync();
        var localSignals = await Task.Run(() => LocalTechnologyDetector.Detect(localRepositories.Where(x => x.IsTracked).Select(x => x.LocalPath)));
        foreach (var language in localSignals.Languages)
            signals.Add(new(SemanticIndexKind.Language, language, $"local-language:{language.ToLowerInvariant()}", language, SemanticIndexSignalOrigin.LocalRepository, !string.IsNullOrWhiteSpace(OfficialTechnologyLinks.Get(language))));
        foreach (var framework in localSignals.Frameworks)
            signals.Add(new(SemanticIndexKind.Framework, framework, $"local-framework:{framework.ToLowerInvariant()}", Origin: SemanticIndexSignalOrigin.LocalRepository, HasOfficialLink: !string.IsNullOrWhiteSpace(OfficialTechnologyLinks.Get(framework))));

        var items = _semanticCatalog.Build(signals).Items;

        SemanticIndexItems.Clear();
        foreach (var item in items) SemanticIndexItems.Add(new(item, _tilePalette.Create(item.AccentKey)));
        SemanticMosaicItems.Clear();
        if (_tileBoard is not null && items.Count > 0)
        {
            var state = await _semanticLayout.SynchronizeAsync(_tileBoard.Id, items, Math.Clamp(_viewportWidth / Math.Max(1, _viewportHeight), 1.1, 2.2));
            SemanticCanvasWidth = state.ExtentColumns * (SemanticMosaicItemViewModel.Unit + SemanticMosaicItemViewModel.Gap) - SemanticMosaicItemViewModel.Gap;
            SemanticCanvasHeight = state.ExtentRows * (SemanticMosaicItemViewModel.Unit + SemanticMosaicItemViewModel.Gap) - SemanticMosaicItemViewModel.Gap;
            foreach (var placement in state.Placements)
            {
                var palette = _tilePalette.Create(placement.Item.AccentKey);
                var color = Color.Parse(palette.Background);
                SemanticMosaicItems.Add(new(placement, new SolidColorBrush(color), new SolidColorBrush(Color.FromArgb(105, color.R, color.G, color.B))));
            }
        }
        OnPropertyChanged(nameof(HasSemanticIndex));
    }

    private DetailTarget CreateDetailTarget(MetroTileViewModel tile)
    {
        var kind = tile.Content.Kind switch
        {
            MetroTileKind.Repository or MetroTileKind.FeaturedRepository => DetailTargetKind.Repository,
            MetroTileKind.Language => DetailTargetKind.Language,
            MetroTileKind.Technology => DetailTargetKind.Framework,
            MetroTileKind.RankingList => DetailTargetKind.Ranking,
            _ => DetailTargetKind.Tip
        };
        var repository = tile.RepositoryItem?.Repository.Repository;
        var facts = new List<DetailFact>();
        if (repository is not null) facts.AddRange([new("Stars", repository.Stars.ToString("N0")), new("Forks", repository.Forks.ToString("N0")), new("语言", repository.PrimaryLanguage)]);
        else if (tile.Ranking is not null) facts.AddRange(tile.Ranking.Items.Take(5).Select(item => new DetailFact($"#{item.Rank}", item.FullName)));
        else if (tile.IsLanguage) facts.AddRange(Tiles.Where(x => x.RepositoryItem?.Repository.PrimaryLanguage.Equals(tile.Title, StringComparison.OrdinalIgnoreCase) == true).Take(6).Select(x => new DetailFact("关联仓库", x.Title)));
        else if (tile.IsTechnology) facts.AddRange(Tiles.Where(x => x.RepositoryItem?.Repository.Topics.Any(topic => topic.Equals(tile.Title, StringComparison.OrdinalIgnoreCase)) == true).Take(6).Select(x => new DetailFact("关联仓库", x.Title)));
        else if (!string.IsNullOrWhiteSpace(tile.Caption)) facts.Add(new("来源", tile.Caption));
        return new(tile.Key, kind, tile.Title, tile.Subtitle, tile.Content.SourceUrl, tile.Content.AccentKey, repository?.Id, repository?.Owner ?? string.Empty, repository?.Name ?? string.Empty, tile.Caption, facts);
    }

    private static string ExplainWorkspaceError(string code) => code switch
    {
        "visual_studio_workspace_missing" => "该仓库没有 Visual Studio 可直接打开的解决方案或项目文件",
        "ide_or_repository_missing" => "IDE 或本地仓库目录已经不存在",
        _ => "无法启动所选 IDE，请重新扫描或更换 IDE"
    };
    private string IdeScope() => _session.Current.User?.Login ?? "guest";
    private static string TechnologyKey(Repository repository) => string.IsNullOrWhiteSpace(repository.PrimaryLanguage) ? "unknown" : repository.PrimaryLanguage;
    private static TileWorldRect Rect(MetroTileViewModel tile) => new(tile.Left, tile.Top, tile.Width, tile.Height);
    private static double DistanceSquared(MetroTileViewModel first, MetroTileViewModel second) { var x = first.Left - second.Left; var y = first.Top - second.Top; return x * x + y * y; }
    private static bool Intersects(TileWorldRect first, TileWorldRect second, double margin) => first.X + first.Width >= second.X - margin && first.X <= second.X + second.Width + margin && first.Y + first.Height >= second.Y - margin && first.Y <= second.Y + second.Height + margin;
    private static double ClampSemanticAxis(double value, double viewport, double content)
    {
        if (viewport <= 0 || content <= 0) return 24;
        if (content <= Math.Max(0, viewport - 48)) return (viewport - content) / 2;
        return Math.Clamp(value, viewport - content - 24, 24);
    }
    private static double Lerp(double start, double end, double progress) => start + (end - start) * progress;
    private static double Smooth(double value) { value = Math.Clamp(value, 0, 1); return value * value * (3 - 2 * value); }
}

public sealed class SemanticIndexItemViewModel
{
    public SemanticIndexItemViewModel(SemanticIndexItem item, TilePalette palette)
    {
        Item = item;
        var color = Color.Parse(palette.Background);
        Accent = new SolidColorBrush(color);
        Border = new SolidColorBrush(Color.FromArgb(105, color.R, color.G, color.B));
    }
    public SemanticIndexItem Item { get; }
    public string Key => Item.Key;
    public string Title => Item.Title;
    public SemanticIndexKind Kind => Item.Kind;
    public int ProjectCount => Item.ProjectCount;
    public IReadOnlyList<string> ContentKeys => Item.ContentKeys;
    public IBrush Accent { get; }
    public IBrush Border { get; }
}
