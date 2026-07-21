using Avalonia;
using Avalonia.Styling;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Recommendation.Services;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class FeedSourceViewModel : ObservableObject
{
    public FeedSourceViewModel(FeedSource source, string title) { Source = source; Title = title; }
    public FeedSource Source { get; }
    public string Title { get; }
    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isSelected;
}

public sealed partial class DiscoverViewModel : ViewModelBase, ISearchablePage, IDisposable
{
    private readonly DiscoveryStore _store;
    private readonly DiscoverySyncService _sync;
    private readonly RepositoryDetailsViewModel _details;
    private readonly ILogger<DiscoverViewModel> _logger;
    private readonly DashboardRailViewModel _dashboard;
    private readonly RepositoryService _repositories;
    private readonly IRecommendationEngine _recommendations;
    private readonly IMetroTileLayoutService _tileLayout;
    private readonly ITilePaletteService _tilePalette;
    private readonly ITileImageService _tileImages;
    private readonly ITipCatalog _tips;
    private readonly IAuthenticationSessionService _session;
    private readonly IZoomableTileLayoutService _zoomLayout;
    private readonly IDetailContentService _detailContent;
    private readonly IExternalLinkService _externalLinks;
    private readonly ISemanticMosaicLayoutService _semanticLayout;
    private readonly ISemanticIndexCatalogService _semanticCatalog;
    private readonly ISpatialTileSearchService _spatialSearch;
    private readonly IVirtualTileWorldService _virtualWorld;
    private readonly ITileWorldPresentationService _worldPresentation;
    private readonly IMarkdownDocumentService _markdown;
    private readonly ISafeMarkdownImageService _markdownImages;
    private readonly ILocalIdeDiscoveryService _ideDiscovery;
    private readonly ILocalRepositoryResolver _localResolver;
    private readonly IRepositoryCloneService _cloneService;
    private readonly IIdeLauncher _ideLauncher;
    private readonly IDetailPortalCoordinator _detailPortal;
    private readonly IIdePreferenceService _idePreferences;
    private readonly IRankingRebuildService _rankingRebuild;
    private readonly List<FeedItemViewModel> _allItems = [];

    public ObservableCollection<FeedItemViewModel> Items { get; } = [];
    public List<MetroTileViewModel> Tiles { get; } = [];
    public IReadOnlyList<MetroTileViewModel> RenderedTiles { get; private set; } = [];
    public IReadOnlyList<VirtualTileSlot> RenderedSkeletonSlots { get; private set; } = [];
    public ObservableCollection<DashboardListViewModel> TopLists => _dashboard.TopLists;
    public IReadOnlyList<FeedSourceViewModel> Sources { get; } =
    [
        new(FeedSource.Trending, "热门"),
        new(FeedSource.ForYou, "为你推荐"),
        new(FeedSource.Subscription, "订阅")
    ];
    public IReadOnlyList<string> SuggestedTopics { get; } = ["AI", "开发工具", "Avalonia", ".NET", "Rust", "TypeScript"];

    [ObservableProperty] private FeedSourceViewModel _selectedSource;
    [ObservableProperty] private FeedItemViewModel? _selectedItem;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusTitle = "还没有发现内容";
    [ObservableProperty] private string _statusDescription = "从热门项目开始，或创建订阅来建立你的长期关注列表。";
    [ObservableProperty] private double _tileCanvasWidth = 1796;
    [ObservableProperty] private double _tileCanvasHeight = 596;
    [ObservableProperty] private double _tileWorldOriginX;
    [ObservableProperty] private double _tileWorldOriginY;
    [ObservableProperty] private double _cameraX;
    [ObservableProperty] private double _cameraY;
    [ObservableProperty] private double _zoom = 1;
    [ObservableProperty] private int _skeletonRevision;
    [ObservableProperty] private string _activeTileFilter = string.Empty;
    private TileBoardState? _tileBoard;
    public bool HasActiveTileFilter => !string.IsNullOrWhiteSpace(ActiveTileFilter);

    public bool IsEmpty => !IsLoading && Items.Count == 0;
    public bool HasItems => Items.Count > 0;
    public string ResultSummary => HasItems ? $"共 {Items.Count} 个项目" : "等待你的第一次发现";
    public string TilePopulationSummary
    {
        get
        {
            var repositories = Tiles.Count(x => x.IsRepository);
            return Items.Count == 0 ? "等待数据池" : $"空间已铺入 {repositories} / {Items.Count}";
        }
    }

    public event EventHandler? LoginRequested;
    public event EventHandler<string>? SubscriptionRequested;

    public DiscoverViewModel(DiscoveryStore store, DiscoverySyncService sync, RepositoryDetailsViewModel details, ILogger<DiscoverViewModel> logger, DashboardRailViewModel dashboard, RepositoryService repositories, IRecommendationEngine recommendations, IMetroTileLayoutService tileLayout, ITilePaletteService tilePalette, ITileImageService tileImages, ITipCatalog tips, IAuthenticationSessionService session, IZoomableTileLayoutService zoomLayout, IDetailContentService detailContent, IExternalLinkService externalLinks, ISemanticMosaicLayoutService semanticLayout, ISemanticIndexCatalogService semanticCatalog, ISpatialTileSearchService spatialSearch, IVirtualTileWorldService virtualWorld, ITileWorldPresentationService worldPresentation, IMarkdownDocumentService markdown, ISafeMarkdownImageService markdownImages, ILocalIdeDiscoveryService ideDiscovery, ILocalRepositoryResolver localResolver, IRepositoryCloneService cloneService, IIdeLauncher ideLauncher, IDetailPortalCoordinator detailPortal, IIdePreferenceService idePreferences, IRankingRebuildService rankingRebuild)
    {
        _store = store;
        _sync = sync;
        _details = details;
        _logger = logger;
        _dashboard = dashboard;
        _repositories = repositories;
        _recommendations = recommendations;
        _tileLayout = tileLayout;
        _tilePalette = tilePalette;
        _tileImages = tileImages;
        _tips = tips;
        _session = session;
        _zoomLayout = zoomLayout;
        _detailContent = detailContent;
        _externalLinks = externalLinks;
        _semanticLayout = semanticLayout;
        _semanticCatalog = semanticCatalog;
        _spatialSearch = spatialSearch;
        _virtualWorld = virtualWorld;
        _worldPresentation = worldPresentation;
        _markdown = markdown;
        _markdownImages = markdownImages;
        _ideDiscovery = ideDiscovery;
        _localResolver = localResolver;
        _cloneService = cloneService;
        _ideLauncher = ideLauncher;
        _detailPortal = detailPortal;
        _idePreferences = idePreferences;
        _rankingRebuild = rankingRebuild;
        _rankingRebuild.Rebuilt += OnRankingRebuilt;
        _selectedSource = Sources[0];
        _selectedSource.IsSelected = true;
    }

    public async Task LoadAsync(bool reflowTileBoard = false)
    {
        SetFocusedTile(null);
        IsLoading = true;
        try
        {
            _allItems.Clear();
            foreach (var item in await _store.GetFeedAsync(SelectedSource.Source)) _allItems.Add(new FeedItemViewModel(item));
            ApplyFilter();
            await RefreshCountsAsync();
            await _dashboard.LoadAsync();
            SetFocusedTile(null);
            await BuildTileBoardAsync(reflow: reflowTileBoard);
            StatusTitle = SelectedSource.Source == FeedSource.Subscription ? "订阅 Feed 还是空的" : "还没有发现内容";
            StatusDescription = SelectedSource.Source == FeedSource.Subscription
                ? "创建主题、语言或关键词订阅，下次同步后内容会出现在这里。"
                : "从热门项目开始，或创建订阅来建立你的长期关注列表。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取发现 Feed 失败");
            StatusTitle = "暂时无法读取 Feed";
            StatusDescription = "本地内容读取失败，请稍后重试。";
            Items.Clear();
        }
        finally
        {
            IsLoading = false;
            NotifyState();
        }
    }

    [RelayCommand]
    public async Task SyncAsync()
    {
        if (IsLoading) return;
        await SaveCameraAsync();
        IsLoading = true;
        NotifyState();
        try
        {
            await _sync.SyncAsync(true);
        }
        finally
        {
            await LoadAsync(reflowTileBoard: true);
        }
    }

    [RelayCommand]
    private async Task SaveAsync(long id)
    {
        var item = _allItems.FirstOrDefault(x => x.Id == id);
        if (item is null) return;
        var isNewSave = !item.Item.Repository.IsBookmarked;
        await _store.ToggleSavedAsync(item.Item.RepositoryId);
        if (isNewSave) await _recommendations.RecordFeedbackAsync(item.Item.RepositoryId, FeedbackType.Bookmark);
        item.ToggleBookmarked();
        if (_details.Repository?.Id == item.Item.Repository.Id) _details.Show(item.Item.Repository, item.Item.Reason);
    }

    [RelayCommand]
    private async Task SaveTileAsync(MetroTileViewModel tile)
    {
        if (tile.RepositoryItem is null) return;
        await SaveAsync(tile.RepositoryItem.Id);
        tile.RefreshSavedState();
    }

    [RelayCommand]
    private async Task DismissAsync(long id)
    {
        var item = _allItems.FirstOrDefault(x => x.Id == id);
        await _store.MarkReadAsync(id, true);
        if (item is not null) await _recommendations.RecordFeedbackAsync(item.Item.RepositoryId, FeedbackType.Dismiss);
        if (item is not null) _allItems.Remove(item);
        ApplyFilter();
        NotifyState();
    }

    [RelayCommand]
    private async Task DismissTileAsync(MetroTileViewModel tile)
    {
        if (tile.RepositoryItem is null) return;
        await DismissAsync(tile.RepositoryItem.Id);
        await BuildTileBoardAsync();
    }

    [RelayCommand]
    private void ActivateTile(MetroTileViewModel tile)
    {
        SetFocusedTile(tile);
        if (tile.RepositoryItem is not null) { SelectedItem = tile.RepositoryItem; return; }
        if (!tile.IsLanguage && !tile.IsTechnology) return;
        ActiveTileFilter = ActiveTileFilter.Equals(tile.Title, StringComparison.OrdinalIgnoreCase) ? string.Empty : tile.Title;
        ApplyTileFilter();
    }

    [RelayCommand]
    private void ClearTileFilter()
    {
        ActiveTileFilter = string.Empty;
        ApplyTileFilter();
        DebounceCameraSave();
    }

    [RelayCommand]
    private void SelectSource(FeedSourceViewModel source)
    {
        _ = SaveViewStateAsync();
        SetFocusedTile(null);
        foreach (var item in Sources) item.IsSelected = item == source;
        SelectedSource = source;
    }

    [RelayCommand]
    private void RequestLogin() => LoginRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task OpenTopItemAsync(DashboardListItem item)
    {
        if (await _repositories.GetByIdAsync(item.RepositoryId) is { } repository) _details.Show(repository, new FeedReason { Summary = item.Caption, Score = repository.DiscoveryScore });
    }

    [RelayCommand]
    private void CreateSubscription(string topic) => SubscriptionRequested?.Invoke(this, topic);

    partial void OnSelectedSourceChanged(FeedSourceViewModel value) => _ = LoadAsync();
    partial void OnSearchTextChanged(string value) { ApplyFilter(); ApplyTileFilter(); ScheduleSpatialSearch(value); }
    partial void OnActiveTileFilterChanged(string value) => OnPropertyChanged(nameof(HasActiveTileFilter));
    partial void OnSelectedItemChanged(FeedItemViewModel? value)
    {
        if (value is null) return;
        _details.Show(value.Item.Repository, value.Item.Reason);
        if (!value.Item.IsRead) _ = RecordExposureAsync(value);
    }

    private async Task RecordExposureAsync(FeedItemViewModel value)
    {
        await _store.MarkReadAsync(value.Id);
        await _recommendations.RecordFeedbackAsync(value.Item.RepositoryId, FeedbackType.View);
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _allItems
            : _allItems.Where(x => x.Repository.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.Repository.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.Repository.PrimaryLanguage.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.ReasonText.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        Items.Clear();
        foreach (var item in filtered) Items.Add(item);
        NotifyState();
    }

    private async Task BuildTileBoardAsync(int minimumColumns = 18, int minimumRows = 10, bool reflow = false)
    {
        var previousBoardId = _tileBoard?.Id;
        var liveCamera = Camera;
        var anchorKey = reflow ? FindCenterAnchorKey() : null;
        var liveSemanticViewport = new SemanticViewportState(SemanticViewportX, SemanticViewportY, _semanticViewportWidth, _semanticViewportHeight, SemanticViewportUserPositioned);
        var content = new List<TileContent>();
        foreach (var list in TopLists)
            content.Add(new($"ranking:{list.Title}", MetroTileKind.RankingList, list.Title, list.Subtitle, "TOP 5", "TypeScript"));

        foreach (var group in _allItems.Where(x => !string.IsNullOrWhiteSpace(x.Repository.PrimaryLanguage)).GroupBy(x => x.Repository.PrimaryLanguage, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Count()).Take(10))
            content.Add(new($"language:{group.Key.ToLowerInvariant()}", MetroTileKind.Language, group.Key, $"{group.Count()} 个关联仓库", "点击筛选", group.Key, SourceUrl: OfficialTechnologyLinks.Get(group.Key)));

        foreach (var group in _allItems.SelectMany(x => x.Repository.Topics.Select(topic => (Topic: topic, Item: x))).Where(x => !string.IsNullOrWhiteSpace(x.Topic)).GroupBy(x => x.Topic, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Count()).Take(10))
            content.Add(new($"stack:{group.Key.ToLowerInvariant()}", MetroTileKind.Technology, group.Key, $"关联 {group.Select(x => x.Item.Id).Distinct().Count()} 个项目", "技术栈", group.Key, SourceUrl: OfficialTechnologyLinks.Get(group.Key)));

        foreach (var item in _allItems)
        {
            var kind = item.Repository.Stars >= 100_000 ? MetroTileKind.FeaturedRepository : MetroTileKind.Repository;
            content.Add(new($"repository:{item.Repository.Id}", kind, item.Repository.FullName, item.Repository.Description, item.ReasonText, item.Repository.PrimaryLanguage, item.Repository.Id, item.Repository.OwnerAvatarUrl, SourceUrl: item.Repository.Repository.HtmlUrl));
        }

        var scope = _session.Current.User?.GitHubId ?? "guest";
        _tileBoard = await _tileLayout.SynchronizeAsync(scope, SelectedSource.Source, content, minimumColumns, minimumRows, reflow);
        var repositoriesById = _allItems.GroupBy(x => x.Repository.Id).ToDictionary(x => x.Key, x => x.First());
        var rankingsByKey = TopLists.ToDictionary(x => $"ranking:{x.Title}", StringComparer.Ordinal);
        Tiles.Clear();
        foreach (var placement in _tileBoard.Placements)
        {
            if (placement.Content.IsPlaceholder) continue;
            repositoriesById.TryGetValue(placement.Content.RepositoryId ?? -1, out var repository);
            rankingsByKey.TryGetValue(placement.Content.Key, out var ranking);
            var tile = new MetroTileViewModel(placement, _tilePalette.Create(placement.Content.AccentKey), repository, ranking);
            Tiles.Add(tile);
            if (tile.IsRepository && !string.IsNullOrWhiteSpace(placement.Content.ImageUrl)) _ = LoadTileImageAsync(tile, placement.Content.ImageUrl);
        }
        var worldSnapshot = _worldPresentation.CreateSnapshot(_tileBoard, anchorKey);
        if (Tiles.Count > 0)
        {
            TileWorldOriginX = worldSnapshot.ContentBounds.Left;
            TileWorldOriginY = worldSnapshot.ContentBounds.Top;
            TileCanvasWidth = Math.Max(1, worldSnapshot.ContentBounds.Width);
            TileCanvasHeight = Math.Max(1, worldSnapshot.ContentBounds.Height);
            foreach (var tile in Tiles) tile.SetWorldOrigin(TileWorldOriginX, TileWorldOriginY);
        }
        else
        {
            TileWorldOriginX = 0;
            TileWorldOriginY = 0;
            TileCanvasWidth = 1;
            TileCanvasHeight = 1;
        }
        RenderedTiles = Tiles.ToArray();
        OnPropertyChanged(nameof(RenderedTiles));
        _renderWindowKey = string.Empty;
        _tileRenderStateKey = string.Empty;
        var camera = previousBoardId == _tileBoard.Id ? liveCamera : new CameraState(_tileBoard.CameraX, _tileBoard.CameraY, _tileBoard.Zoom, ActiveIndexKind: _tileBoard.ActiveIndexKind, ActiveIndexKey: _tileBoard.ActiveIndexKey);
        CameraX = camera.X; CameraY = camera.Y; Zoom = camera.Zoom;
        if (reflow)
            RestoreReflowAnchor(anchorKey);
        var semanticViewport = previousBoardId == _tileBoard.Id
            ? liveSemanticViewport
            : new(_tileBoard.SemanticViewportX, _tileBoard.SemanticViewportY,
                _tileBoard.SemanticViewportWidth, _tileBoard.SemanticViewportHeight,
                _tileBoard.SemanticViewportUserPositioned);
        SemanticViewportX = semanticViewport.X;
        SemanticViewportY = semanticViewport.Y;
        _semanticViewportWidth = semanticViewport.ViewportWidth;
        _semanticViewportHeight = semanticViewport.ViewportHeight;
        SemanticViewportUserPositioned = semanticViewport.IsUserPositioned;
        ActiveTileFilter = camera.ActiveIndexKey ?? string.Empty;
        await BuildSemanticIndexAsync();
        ApplyTileFilter();
        UpdateCameraPresentation();
        OnPropertyChanged(nameof(TilePopulationSummary));
    }

    private string? FindCenterAnchorKey()
    {
        if (Tiles.Count == 0) return null;
        var zoom = Math.Max(_zoomLayout.ScaleProfile.MinimumZoom, Zoom);
        var centerX = CameraX + _viewportWidth / (2 * zoom);
        var centerY = CameraY + _viewportHeight / (2 * zoom);
        return Tiles
            .Where(x => x.IsRepository)
            .OrderBy(x => DistanceSquared(x.Left + x.Width / 2, x.Top + x.Height / 2, centerX, centerY))
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .FirstOrDefault()?.Key;
    }

    private void RestoreReflowAnchor(string? anchorKey)
    {
        var zoom = Math.Max(_zoomLayout.ScaleProfile.MinimumZoom, Zoom);
        var target = string.IsNullOrWhiteSpace(anchorKey)
            ? null
            : Tiles.FirstOrDefault(x => x.Key.Equals(anchorKey, StringComparison.Ordinal));
        var centerX = target is null
            ? TileWorldOriginX + TileCanvasWidth / 2
            : target.Left + target.Width / 2;
        var centerY = target is null
            ? TileWorldOriginY + TileCanvasHeight / 2
            : target.Top + target.Height / 2;
        CameraX = centerX - _viewportWidth / (2 * zoom);
        CameraY = centerY - _viewportHeight / (2 * zoom);
    }

    private static double DistanceSquared(double x, double y, double targetX, double targetY)
    {
        var dx = x - targetX;
        var dy = y - targetY;
        return dx * dx + dy * dy;
    }

    private async Task LoadTileImageAsync(MetroTileViewModel tile, string url)
    {
        if (await _tileImages.GetAsync(url) is { } asset) tile.ApplyImageAsset(asset, _tilePalette);
    }

    private void ApplyTileFilter()
    {
        var filter = string.IsNullOrWhiteSpace(ActiveTileFilter) ? SearchText : ActiveTileFilter;
        foreach (var tile in Tiles) tile.SetFilter(filter);
    }

    private async Task RefreshCountsAsync()
    {
        foreach (var source in Sources) source.Count = (await _store.GetFeedAsync(source.Source)).Count;
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ResultSummary));
        OnPropertyChanged(nameof(TilePopulationSummary));
    }
}

public sealed class SubscriptionItemViewModel
{
    public SubscriptionItemViewModel(DiscoverySubscription item) => Item = item;
    public DiscoverySubscription Item { get; }
    public long Id => Item.Id;
    public string Name => Item.Name;
    public bool IsEnabled => Item.IsEnabled;
    public string StateText => Item.IsEnabled ? "已启用" : "已暂停";
    public string RulesText
    {
        get
        {
            var values = Item.Topics.Select(x => $"主题：{x}")
                .Concat(Item.Languages.Select(x => $"语言：{x}"))
                .Concat(Item.Keywords.Select(x => $"关键词：{x}"));
            var text = string.Join("  ·  ", values);
            return string.IsNullOrWhiteSpace(text) ? "尚未配置匹配规则" : text;
        }
    }
    public string LastSyncText => Item.LastSyncedAt is null ? "尚未同步" : $"上次同步 {Item.LastSyncedAt.Value.LocalDateTime:yyyy-MM-dd HH:mm}";
}

public sealed partial class SubscriptionsViewModel : ViewModelBase, ISearchablePage
{
    private readonly DiscoveryStore _store;
    private readonly List<SubscriptionItemViewModel> _allItems = [];
    public ObservableCollection<SubscriptionItemViewModel> Items { get; } = [];
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _topics = string.Empty;
    [ObservableProperty] private string _languages = string.Empty;
    [ObservableProperty] private string _keywords = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _validationMessage = string.Empty;
    public bool IsEmpty => !IsLoading && Items.Count == 0;

    public SubscriptionsViewModel(DiscoveryStore store) => _store = store;

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _allItems.Clear();
            foreach (var item in await _store.GetSubscriptionsAsync()) _allItems.Add(new SubscriptionItemViewModel(item));
            ApplyFilter();
        }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    public void PrefillTopic(string topic)
    {
        Name = $"关注 {topic}";
        Topics = topic;
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { ValidationMessage = "请先填写订阅名称。"; return; }
        if (string.IsNullOrWhiteSpace(Topics) && string.IsNullOrWhiteSpace(Languages) && string.IsNullOrWhiteSpace(Keywords))
        { ValidationMessage = "至少填写一个主题、语言或关键词。"; return; }
        await _store.SaveSubscriptionAsync(new DiscoverySubscription { Name = Name, Topics = Split(Topics), Languages = Split(Languages), Keywords = Split(Keywords) });
        Name = Topics = Languages = Keywords = ValidationMessage = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ToggleAsync(SubscriptionItemViewModel item)
    {
        item.Item.IsEnabled = !item.Item.IsEnabled;
        await _store.SaveSubscriptionAsync(item.Item);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(long id) { await _store.DeleteSubscriptionAsync(id); await LoadAsync(); }
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        Items.Clear();
        foreach (var item in _allItems.Where(x => string.IsNullOrEmpty(query) || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.RulesText.Contains(query, StringComparison.OrdinalIgnoreCase))) Items.Add(item);
        OnPropertyChanged(nameof(IsEmpty));
    }
    private static List<string> Split(string input) => input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public sealed partial class LibraryViewModel : ViewModelBase, ISearchablePage
{
    private readonly DiscoveryStore _store;
    private readonly RepositoryDetailsViewModel _details;
    private readonly List<RepositoryViewModel> _allItems = [];
    public ObservableCollection<RepositoryViewModel> Items { get; } = [];
    public ObservableCollection<string> Languages { get; } = ["全部语言"];
    public IReadOnlyList<string> SortOptions { get; } = ["最近收藏", "Stars 最多", "最近更新", "名称"];
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedLanguage = "全部语言";
    [ObservableProperty] private string _selectedSort = "最近收藏";
    [ObservableProperty] private RepositoryViewModel? _selectedItem;
    [ObservableProperty] private bool _isLoading;
    public bool IsEmpty => !IsLoading && Items.Count == 0;
    public string Status => $"已收藏 {Items.Count} 个仓库";

    public LibraryViewModel(DiscoveryStore store, RepositoryDetailsViewModel details) { _store = store; _details = details; }
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _allItems.Clear();
            foreach (var item in await _store.GetSavedRepositoriesAsync()) _allItems.Add(new RepositoryViewModel(item));
            Languages.Clear(); Languages.Add("全部语言");
            foreach (var language in _allItems.Select(x => x.PrimaryLanguage).Distinct(StringComparer.OrdinalIgnoreCase).Order()) Languages.Add(language);
            ApplyFilter();
        }
        finally { IsLoading = false; NotifyState(); }
    }
    [RelayCommand] private async Task RemoveAsync(long id) { await _store.ToggleSavedAsync(id); if (_details.Repository?.Id == id) _details.Close(); await LoadAsync(); }
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedLanguageChanged(string value) => ApplyFilter();
    partial void OnSelectedSortChanged(string value) => ApplyFilter();
    partial void OnSelectedItemChanged(RepositoryViewModel? value) { if (value is not null) _details.Show(value.Repository); }
    private void ApplyFilter()
    {
        IEnumerable<RepositoryViewModel> query = _allItems;
        if (!string.IsNullOrWhiteSpace(SearchText)) query = query.Where(x => x.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        if (SelectedLanguage != "全部语言") query = query.Where(x => x.PrimaryLanguage.Equals(SelectedLanguage, StringComparison.OrdinalIgnoreCase));
        query = SelectedSort switch { "Stars 最多" => query.OrderByDescending(x => x.Stars), "最近更新" => query.OrderByDescending(x => x.Repository.UpdatedAt), "名称" => query.OrderBy(x => x.FullName), _ => query };
        Items.Clear(); foreach (var item in query) Items.Add(item); NotifyState();
    }
    private void NotifyState() { OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(Status)); }
}

public sealed partial class NotificationsViewModel : ViewModelBase, ISearchablePage
{
    private readonly DiscoveryStore _store;
    private readonly RepositoryDetailsViewModel _details;
    private readonly List<FeedItemViewModel> _allItems = [];
    public ObservableCollection<FeedItemViewModel> Items { get; } = [];
    public IReadOnlyList<string> Filters { get; } = ["全部", "未读", "已处理"];
    [ObservableProperty] private string _selectedFilter = "全部";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private FeedItemViewModel? _selectedItem;
    [ObservableProperty] private bool _isLoading;
    public int UnreadCount => _allItems.Count(x => !x.Item.IsRead);
    public bool IsEmpty => !IsLoading && Items.Count == 0;

    public NotificationsViewModel(DiscoveryStore store, RepositoryDetailsViewModel details) { _store = store; _details = details; }
    public async Task LoadAsync()
    {
        IsLoading = true;
        try { _allItems.Clear(); foreach (var item in await _store.GetNotificationsAsync()) _allItems.Add(new FeedItemViewModel(item)); ApplyFilter(); }
        finally { IsLoading = false; NotifyState(); }
    }
    [RelayCommand] private async Task ReadAsync(long id) { await _store.MarkReadAsync(id); var item = _allItems.FirstOrDefault(x => x.Id == id); if (item is not null) item.Item.IsRead = true; ApplyFilter(); }
    [RelayCommand] private async Task MarkAllReadAsync() { foreach (var item in _allItems.Where(x => !x.Item.IsRead)) await _store.MarkReadAsync(item.Id); await LoadAsync(); }
    partial void OnSelectedFilterChanged(string value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedItemChanged(FeedItemViewModel? value) { if (value is not null) _details.Show(value.Item.Repository, value.Item.Reason); }
    private void ApplyFilter()
    {
        IEnumerable<FeedItemViewModel> query = _allItems;
        query = SelectedFilter switch { "未读" => query.Where(x => !x.Item.IsRead), "已处理" => query.Where(x => x.Item.IsRead), _ => query };
        if (!string.IsNullOrWhiteSpace(SearchText)) query = query.Where(x => x.Repository.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || x.ReasonText.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        Items.Clear(); foreach (var item in query) Items.Add(item); NotifyState();
    }
    private void NotifyState() { OnPropertyChanged(nameof(UnreadCount)); OnPropertyChanged(nameof(IsEmpty)); }
}

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserService _users;
    private readonly ICacheService _cache;
    private readonly IMemoryCacheStore _memoryCache;
    private readonly IDbContextFactory<RepoGalaxyDbContext> _databaseFactory;
    private readonly DatabaseLifecycleService _databaseLifecycle;
    private readonly DiscoverySyncService _syncService;
    private readonly IMetroTileLayoutService _tileLayout;
    private readonly IAuthenticationSessionService _session;
    private readonly IRankingConfigurationService _rankingConfiguration;
    private readonly IRankingRebuildService _rankingRebuild;
    private readonly DiscoverViewModel _discover;
    private UserPreference? _preferences;
    private bool _isLoaded;
    private bool _loadingRanking;
    private CancellationTokenSource? _rankingRebuildCancellation;
    public IReadOnlyList<string> ThemeOptions { get; } = ["跟随系统", "浅色", "深色"];
    public IReadOnlyList<SyncIntervalOption> SyncIntervals { get; } = [new(0, "仅手动"), new(15, "每 15 分钟"), new(30, "每 30 分钟"), new(60, "每 60 分钟")];
    public IReadOnlyList<string> CachePresets { get; } = ["节省", "均衡", "高性能", "自定义"];
    [ObservableProperty] private string _selectedTheme = "跟随系统";
    [ObservableProperty] private SyncIntervalOption _selectedSyncInterval = new(30, "每 30 分钟");
    [ObservableProperty] private double _notificationThreshold = .75;
    [ObservableProperty] private string _saveStatus = string.Empty;
    [ObservableProperty] private string _selectedCachePreset = "均衡";
    [ObservableProperty] private int _memoryCacheSizeMb = 256;
    [ObservableProperty] private int _persistentCacheSizeMb = 1024;
    [ObservableProperty] private int _feedCacheTtlMinutes = 30;
    [ObservableProperty] private int _detailCacheTtlMinutes = 360;
    [ObservableProperty] private int _newsCacheTtlMinutes = 30;
    [ObservableProperty] private string _cacheStatus = "正在读取缓存统计";
    [ObservableProperty] private string _identityName = string.Empty;
    [ObservableProperty] private string _identityEmail = string.Empty;
    [ObservableProperty] private string _identityStatus = string.Empty;
    [ObservableProperty] private string _backupStatus = "正在读取备份状态";
    [ObservableProperty] private string _tileLayoutStatus = string.Empty;
    public ObservableCollection<GitIdentityAliasItem> IdentityAliases { get; } = [];
    public IReadOnlyList<RankingPresetOption> RankingPresets { get; } =
    [
        new(RankingPreset.Balanced, "均衡"), new(RankingPreset.Precision, "精准"),
        new(RankingPreset.Exploration, "探索"), new(RankingPreset.Custom, "自定义")
    ];
    [ObservableProperty] private RankingPresetOption _selectedRankingPreset = new(RankingPreset.Balanced, "均衡");
    [ObservableProperty] private bool _isRankingAdvanced;
    [ObservableProperty] private double _coarseRuleMatch = 30;
    [ObservableProperty] private double _coarseFreshness = 20;
    [ObservableProperty] private double _coarseStarVelocity = 20;
    [ObservableProperty] private double _coarseQuality = 15;
    [ObservableProperty] private double _coarsePreference = 15;
    [ObservableProperty] private double _fineCoarse = 45;
    [ObservableProperty] private double _fineContentProfile = 20;
    [ObservableProperty] private double _fineBehavior = 15;
    [ObservableProperty] private double _fineNovelty = 10;
    [ObservableProperty] private double _fineLocalRelevance = 10;
    [ObservableProperty] private double _explorationRatio = 15;
    [ObservableProperty] private double _rankingTemperature = 1;
    [ObservableProperty] private double _freshnessHalfLifeDays = 120;
    [ObservableProperty] private int _sameLanguagePerTen = 3;
    [ObservableProperty] private int _sameOwnerPerTen = 1;
    [ObservableProperty] private int _coarseCandidateCount = 200;
    [ObservableProperty] private int _fineResultCount = 60;
    [ObservableProperty] private string _rankingStatus = string.Empty;
    [ObservableProperty] private double _rankingProgress;
    [ObservableProperty] private bool _isRankingRebuilding;

    public string ThresholdText => $"匹配度达到 {NotificationThreshold:P0} 时提醒";
    public string RankingScopeText => _session.Current.User is { } user ? $"当前账号：@{user.Login}" : "游客配置";
    public string CoarseWeightTotalText => $"粗排合计 {CoarseRuleMatch + CoarseFreshness + CoarseStarVelocity + CoarseQuality + CoarsePreference:N0}%";
    public string FineWeightTotalText => $"精排合计 {FineCoarse + FineContentProfile + FineBehavior + FineNovelty + FineLocalRelevance:N0}%";
    public SettingsViewModel(IUserService users, ICacheService cache, IMemoryCacheStore memoryCache, IDbContextFactory<RepoGalaxyDbContext> databaseFactory, DatabaseLifecycleService databaseLifecycle, DiscoverySyncService syncService, IMetroTileLayoutService tileLayout, IAuthenticationSessionService session, IRankingConfigurationService rankingConfiguration, IRankingRebuildService rankingRebuild, DiscoverViewModel discover)
    {
        _users = users; _cache = cache; _memoryCache = memoryCache; _databaseFactory = databaseFactory; _databaseLifecycle = databaseLifecycle;
        _syncService = syncService; _tileLayout = tileLayout; _session = session; _rankingConfiguration = rankingConfiguration; _rankingRebuild = rankingRebuild; _discover = discover;
        _session.Changed += (_, _) => { OnPropertyChanged(nameof(RankingScopeText)); _ = LoadRankingProfileAsync(); };
    }
    public async Task LoadAsync()
    {
        _preferences = await _users.GetPreferencesAsync();
        SelectedTheme = _preferences.UseSystemTheme ? "跟随系统" : _preferences.DarkMode ? "深色" : "浅色";
        SelectedSyncInterval = SyncIntervals.FirstOrDefault(x => x.Minutes == _preferences.SyncIntervalMinutes) ?? SyncIntervals[2];
        NotificationThreshold = _preferences.NotificationThreshold;
        SelectedCachePreset = _preferences.CachePreset;
        MemoryCacheSizeMb = _preferences.MemoryCacheSizeMB;
        PersistentCacheSizeMb = _preferences.PersistentCacheSizeMB;
        FeedCacheTtlMinutes = _preferences.FeedCacheTtlMinutes;
        DetailCacheTtlMinutes = _preferences.DetailCacheTtlMinutes;
        NewsCacheTtlMinutes = _preferences.NewsCacheTtlMinutes;
        _memoryCache.SetSizeLimit((long)MemoryCacheSizeMb * 1024 * 1024);
        _cache.SetPersistentSizeLimit((long)PersistentCacheSizeMb * 1024 * 1024);
        _syncService.UpdateInterval(SelectedSyncInterval.Minutes == 0 ? null : TimeSpan.FromMinutes(SelectedSyncInterval.Minutes));
        _isLoaded = true;
        ApplyTheme();
        await RefreshCacheStatisticsAsync();
        await LoadIdentityAliasesAsync();
        await LoadRankingProfileAsync();
        BackupStatus = _databaseLifecycle.GetLatestBackupPath() is { } path ? $"最近备份：{Path.GetFileName(path)}" : "尚未创建备份";
    }
    partial void OnSelectedThemeChanged(string value) { ApplyTheme(); _ = SaveAsync(); }
    partial void OnSelectedSyncIntervalChanged(SyncIntervalOption value) { _syncService.UpdateInterval(value.Minutes == 0 ? null : TimeSpan.FromMinutes(value.Minutes)); _ = SaveAsync(); }
    partial void OnNotificationThresholdChanged(double value) { OnPropertyChanged(nameof(ThresholdText)); _ = SaveAsync(); }
    partial void OnSelectedCachePresetChanged(string value)
    {
        if (!_isLoaded || value == "自定义") return;
        var preset = value switch { "节省" => (Memory: 128, Disk: 512, Interval: 60), "高性能" => (Memory: 512, Disk: 2048, Interval: 15), _ => (Memory: 256, Disk: 1024, Interval: 30) };
        MemoryCacheSizeMb = preset.Memory; PersistentCacheSizeMb = preset.Disk; SelectedSyncInterval = SyncIntervals.First(x => x.Minutes == preset.Interval);
        FeedCacheTtlMinutes = value == "节省" ? 60 : value == "高性能" ? 15 : 30;
        _ = SaveAsync();
    }
    partial void OnMemoryCacheSizeMbChanged(int value) { if (_isLoaded) { _memoryCache.SetSizeLimit((long)Math.Clamp(value, 64, 1024) * 1024 * 1024); _ = SaveAsync(); } }
    partial void OnPersistentCacheSizeMbChanged(int value) { if (_isLoaded) { _cache.SetPersistentSizeLimit((long)Math.Clamp(value, 256, 4096) * 1024 * 1024); _ = SaveAsync(); } }
    partial void OnFeedCacheTtlMinutesChanged(int value) { if (_isLoaded) _ = SaveAsync(); }
    partial void OnDetailCacheTtlMinutesChanged(int value) { if (_isLoaded) _ = SaveAsync(); }
    partial void OnNewsCacheTtlMinutesChanged(int value) { if (_isLoaded) _ = SaveAsync(); }
    partial void OnSelectedRankingPresetChanged(RankingPresetOption value)
    {
        if (_loadingRanking || value.Preset == RankingPreset.Custom) return;
        ApplyRankingProfile(RankingTuningProfile.Create(RankingScope(), value.Preset));
    }
    partial void OnCoarseRuleMatchChanged(double value) => RankingWeightChanged();
    partial void OnCoarseFreshnessChanged(double value) => RankingWeightChanged();
    partial void OnCoarseStarVelocityChanged(double value) => RankingWeightChanged();
    partial void OnCoarseQualityChanged(double value) => RankingWeightChanged();
    partial void OnCoarsePreferenceChanged(double value) => RankingWeightChanged();
    partial void OnFineCoarseChanged(double value) => RankingWeightChanged();
    partial void OnFineContentProfileChanged(double value) => RankingWeightChanged();
    partial void OnFineBehaviorChanged(double value) => RankingWeightChanged();
    partial void OnFineNoveltyChanged(double value) => RankingWeightChanged();
    partial void OnFineLocalRelevanceChanged(double value) => RankingWeightChanged();
    private void ApplyTheme()
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = SelectedTheme switch { "浅色" => ThemeVariant.Light, "深色" => ThemeVariant.Dark, _ => ThemeVariant.Default };
    }
    private async Task SaveAsync()
    {
        if (!_isLoaded || _preferences is null) return;
        _preferences.UseSystemTheme = SelectedTheme == "跟随系统";
        _preferences.DarkMode = SelectedTheme == "深色";
        _preferences.SyncIntervalMinutes = SelectedSyncInterval.Minutes;
        _preferences.NotificationThreshold = NotificationThreshold;
        _preferences.CachePreset = SelectedCachePreset;
        _preferences.MemoryCacheSizeMB = Math.Clamp(MemoryCacheSizeMb, 64, 1024);
        _preferences.PersistentCacheSizeMB = Math.Clamp(PersistentCacheSizeMb, 256, 4096);
        _preferences.FeedCacheTtlMinutes = Math.Clamp(FeedCacheTtlMinutes, 5, 1440);
        _preferences.DetailCacheTtlMinutes = Math.Clamp(DetailCacheTtlMinutes, 5, 1440);
        _preferences.NewsCacheTtlMinutes = Math.Clamp(NewsCacheTtlMinutes, 5, 1440);
        SaveStatus = await _users.SavePreferencesAsync(_preferences) ? "设置已保存" : "设置保存失败";
    }

    [RelayCommand]
    private async Task SaveRankingProfileAsync()
    {
        var profile = BuildRankingProfile();
        if (!profile.IsValid)
        {
            RankingStatus = "粗排和精排权重必须分别合计为 100%。";
            return;
        }
        try
        {
            var saved = await _rankingConfiguration.SaveAsync(profile);
            ApplyRankingProfile(saved);
            RankingStatus = $"排序参数已保存（版本 {saved.Revision}），相关批次已标记为待更新。";
        }
        catch { RankingStatus = "排序参数保存失败，旧配置仍然有效。"; }
    }

    [RelayCommand]
    private void NormalizeRankingWeights()
    {
        ApplyRankingProfile(BuildRankingProfile().NormalizeWeights());
        SelectedRankingPreset = RankingPresets.Single(x => x.Preset == RankingPreset.Custom);
        RankingStatus = "权重已按比例归一化，请保存后生效。";
    }

    [RelayCommand]
    private async Task RebuildCurrentFeedAsync()
    {
        if (IsRankingRebuilding) return;
        await SaveRankingProfileAsync();
        if (!BuildRankingProfile().IsValid) return;
        _rankingRebuildCancellation?.Cancel(); _rankingRebuildCancellation?.Dispose();
        var cancellation = _rankingRebuildCancellation = new CancellationTokenSource();
        IsRankingRebuilding = true; RankingProgress = 0;
        var progress = new Progress<RankingRebuildProgress>(value => { RankingProgress = value.Progress * 100; RankingStatus = value.Message; });
        try
        {
            var source = _discover.SelectedSource.Source;
            var result = await _rankingRebuild.RebuildAsync(new(RankingScope(), source), progress, cancellation.Token);
            RankingStatus = result.Cancelled ? "已取消重排，旧批次保持不变。" : result.Success ? $"重排完成，共更新 {result.Items.Count} 个项目。" : "重排失败，旧批次保持不变。";
        }
        finally { IsRankingRebuilding = false; if (ReferenceEquals(_rankingRebuildCancellation, cancellation)) _rankingRebuildCancellation = null; cancellation.Dispose(); }
    }

    [RelayCommand] private void CancelRankingRebuild() => _rankingRebuildCancellation?.Cancel();

    private async Task LoadRankingProfileAsync()
    {
        try { ApplyRankingProfile(await _rankingConfiguration.GetAsync(RankingScope())); }
        catch { RankingStatus = "无法读取排序配置，正在使用均衡默认值。"; ApplyRankingProfile(RankingTuningProfile.Create(RankingScope(), RankingPreset.Balanced)); }
    }

    private RankingTuningProfile BuildRankingProfile() => new(RankingScope(), SelectedRankingPreset.Preset,
        new(CoarseRuleMatch / 100, CoarseFreshness / 100, CoarseStarVelocity / 100, CoarseQuality / 100, CoarsePreference / 100),
        new(FineCoarse / 100, FineContentProfile / 100, FineBehavior / 100, FineNovelty / 100, FineLocalRelevance / 100),
        ExplorationRatio / 100, RankingTemperature, FreshnessHalfLifeDays, SameLanguagePerTen, SameOwnerPerTen, CoarseCandidateCount, FineResultCount);

    private void ApplyRankingProfile(RankingTuningProfile profile)
    {
        _loadingRanking = true;
        SelectedRankingPreset = RankingPresets.Single(x => x.Preset == profile.Preset);
        CoarseRuleMatch = profile.Coarse.RuleMatch * 100; CoarseFreshness = profile.Coarse.Freshness * 100; CoarseStarVelocity = profile.Coarse.StarVelocity * 100;
        CoarseQuality = profile.Coarse.Quality * 100; CoarsePreference = profile.Coarse.PreferenceAffinity * 100;
        FineCoarse = profile.Fine.CoarseScore * 100; FineContentProfile = profile.Fine.ContentProfile * 100; FineBehavior = profile.Fine.Behavior * 100;
        FineNovelty = profile.Fine.Novelty * 100; FineLocalRelevance = profile.Fine.LocalRelevance * 100;
        ExplorationRatio = profile.ExplorationRatio * 100; RankingTemperature = profile.Temperature; FreshnessHalfLifeDays = profile.FreshnessHalfLifeDays;
        SameLanguagePerTen = profile.SameLanguagePerTen; SameOwnerPerTen = profile.SameOwnerPerTen; CoarseCandidateCount = profile.CoarseCandidateCount; FineResultCount = profile.FineResultCount;
        _loadingRanking = false;
        OnPropertyChanged(nameof(CoarseWeightTotalText)); OnPropertyChanged(nameof(FineWeightTotalText));
    }

    private string RankingScope() => _session.Current.User?.GitHubId ?? "guest";
    private void RankingWeightChanged()
    {
        OnPropertyChanged(nameof(CoarseWeightTotalText)); OnPropertyChanged(nameof(FineWeightTotalText));
        if (!_loadingRanking) SelectedRankingPreset = RankingPresets.Single(x => x.Preset == RankingPreset.Custom);
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        await _memoryCache.ClearAsync();
        var removed = await _cache.PruneAsync(0);
        CacheStatus = $"已清理 {removed / 1024d / 1024d:N1} MB 网络缓存；Feed 与 Tile 布局按设计保留";
    }
    [RelayCommand] private Task RefreshCacheStatisticsAsync() => RefreshCacheStatisticsCoreAsync();
    private async Task RefreshCacheStatisticsCoreAsync() { var stats = await _cache.GetStatisticsAsync(); var cleaned = stats.LastPrunedAt is null ? "尚未清理" : $"上次清理 {stats.LastPrunedAt.Value.LocalDateTime:MM-dd HH:mm}"; CacheStatus = $"内存 {stats.MemoryBytes / 1024d / 1024d:N1} MB · 磁盘 {stats.PersistentBytes / 1024d / 1024d:N1} MB · 命中率 {stats.HitRate:P0} · stale {stats.StaleHits:N0} · {cleaned}"; }

    [RelayCommand]
    private async Task AddIdentityAliasAsync()
    {
        var name = IdentityName.Trim(); var email = IdentityEmail.Trim();
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email)) { IdentityStatus = "请填写姓名或邮箱。"; return; }
        await using var db = await _databaseFactory.CreateDbContextAsync();
        if (await db.GitIdentityAliases.AnyAsync(x => x.Name == name && x.Email == email)) { IdentityStatus = "这个身份别名已经存在。"; return; }
        db.GitIdentityAliases.Add(new GitIdentityAliasEntity { Name = string.IsNullOrWhiteSpace(name) ? null : name, Email = string.IsNullOrWhiteSpace(email) ? null : email, IsEnabled = true });
        await db.SaveChangesAsync(); IdentityName = string.Empty; IdentityEmail = string.Empty; IdentityStatus = "身份别名已添加，贡献热力图将在下次刷新时更新。"; await LoadIdentityAliasesAsync();
    }
    [RelayCommand]
    private async Task RemoveIdentityAliasAsync(long id)
    {
        await using var db = await _databaseFactory.CreateDbContextAsync();
        await db.GitIdentityAliases.Where(x => x.Id == id).ExecuteDeleteAsync();
        IdentityStatus = "身份别名已移除。"; await LoadIdentityAliasesAsync();
    }
    private async Task LoadIdentityAliasesAsync()
    {
        await using var db = await _databaseFactory.CreateDbContextAsync();
        var rows = await db.GitIdentityAliases.AsNoTracking().OrderBy(x => x.Email).ToListAsync();
        IdentityAliases.Clear(); foreach (var row in rows) IdentityAliases.Add(new GitIdentityAliasItem(row.Id, row.Name ?? string.Empty, row.Email ?? string.Empty));
    }
    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var path = await _databaseLifecycle.CreateDailyBackupAsync();
        BackupStatus = path is null ? "备份创建失败。" : $"备份已就绪：{Path.GetFileName(path)}";
    }
    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        await _syncService.StopAsync();
        BackupStatus = await _databaseLifecycle.RestoreLatestBackupAsync() ? "恢复成功，请重新启动 RepoGalaxy。" : "没有可用的完整备份。";
    }

    [RelayCommand]
    private async Task ResetTileLayoutAsync()
    {
        var scope = _session.Current.User?.GitHubId ?? "guest";
        await _tileLayout.ResetAsync(scope);
        await _discover.LoadAsync();
        TileLayoutStatus = "首页 Tile 布局已重置并立即重新生成。";
    }
}

public sealed record GitIdentityAliasItem(long Id, string Name, string Email)
{
    public string Display => string.Join(" · ", new[] { Name, Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
public sealed record SyncIntervalOption(int Minutes, string Label);
public sealed record RankingPresetOption(RankingPreset Preset, string Label);
