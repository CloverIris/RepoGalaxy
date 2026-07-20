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

public sealed partial class DiscoverViewModel : ViewModelBase, ISearchablePage
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
    private readonly List<FeedItemViewModel> _allItems = [];

    public ObservableCollection<FeedItemViewModel> Items { get; } = [];
    public ObservableCollection<MetroTileViewModel> Tiles { get; } = [];
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
    [ObservableProperty] private double _tileViewportX;
    [ObservableProperty] private double _tileViewportY;
    [ObservableProperty] private string _activeTileFilter = string.Empty;
    private TileBoardState? _tileBoard;
    public bool HasActiveTileFilter => !string.IsNullOrWhiteSpace(ActiveTileFilter);

    public bool IsEmpty => !IsLoading && Items.Count == 0;
    public bool HasItems => Items.Count > 0;
    public string ResultSummary => HasItems ? $"共 {Items.Count} 个项目" : "等待你的第一次发现";

    public event EventHandler? LoginRequested;
    public event EventHandler<string>? SubscriptionRequested;

    public DiscoverViewModel(DiscoveryStore store, DiscoverySyncService sync, RepositoryDetailsViewModel details, ILogger<DiscoverViewModel> logger, DashboardRailViewModel dashboard, RepositoryService repositories, IRecommendationEngine recommendations, IMetroTileLayoutService tileLayout, ITilePaletteService tilePalette, ITileImageService tileImages, ITipCatalog tips, IAuthenticationSessionService session)
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
        _selectedSource = Sources[0];
        _selectedSource.IsSelected = true;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _allItems.Clear();
            foreach (var item in await _store.GetFeedAsync(SelectedSource.Source)) _allItems.Add(new FeedItemViewModel(item));
            ApplyFilter();
            await RefreshCountsAsync();
            await _dashboard.LoadAsync();
            await BuildTileBoardAsync();
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
        IsLoading = true;
        NotifyState();
        try
        {
            await _sync.SyncAsync(true);
            await _sync.RefreshForYouAsync();
        }
        finally
        {
            await LoadAsync();
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
        if (tile.RepositoryItem is not null) { SelectedItem = tile.RepositoryItem; return; }
        if (!tile.IsLanguage && !tile.IsTechnology) return;
        ActiveTileFilter = ActiveTileFilter.Equals(tile.Title, StringComparison.OrdinalIgnoreCase) ? string.Empty : tile.Title;
        ApplyTileFilter();
    }

    [RelayCommand]
    private void ClearTileFilter() { ActiveTileFilter = string.Empty; ApplyTileFilter(); }

    [RelayCommand]
    private void SelectSource(FeedSourceViewModel source)
    {
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
    partial void OnSearchTextChanged(string value) { ApplyFilter(); ApplyTileFilter(); }
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

    public async Task EnsureTileExtentAsync(double viewportWidth, double viewportHeight)
    {
        if (_tileBoard is null || viewportWidth <= 0 || viewportHeight <= 0) return;
        var columns = Math.Max(12, (int)Math.Ceiling(viewportWidth / 100d) + 6);
        var rows = Math.Max(6, (int)Math.Ceiling(viewportHeight / 100d) + 2);
        if (columns <= _tileBoard.ExtentColumns && rows <= _tileBoard.ExtentRows) return;
        await BuildTileBoardAsync(columns, rows);
    }

    public void UpdateTileEdgeOpacity(double viewportX, double viewportY, double viewportWidth, double viewportHeight)
    {
        TileViewportX = Math.Max(0, viewportX); TileViewportY = Math.Max(0, viewportY);
        foreach (var tile in Tiles)
        {
            var visibleWidth = Math.Max(0, Math.Min(tile.Left + tile.Width, viewportX + viewportWidth) - Math.Max(tile.Left, viewportX));
            var visibleHeight = Math.Max(0, Math.Min(tile.Top + tile.Height, viewportY + viewportHeight) - Math.Max(tile.Top, viewportY));
            var ratio = tile.Width <= 0 || tile.Height <= 0 ? 0 : visibleWidth * visibleHeight / (tile.Width * tile.Height);
            tile.SetEdgeOpacity(ratio >= .999 ? 1 : .3 + .7 * ratio);
        }
    }

    public Task SaveTileViewportAsync(double x, double y) => _tileBoard is null ? Task.CompletedTask : _tileLayout.SaveViewportAsync(_tileBoard.Id, x, y);

    private async Task BuildTileBoardAsync(int minimumColumns = 18, int minimumRows = 6)
    {
        var content = new List<TileContent>();
        foreach (var list in TopLists)
            content.Add(new($"ranking:{list.Title}", MetroTileKind.RankingList, list.Title, list.Subtitle, "TOP 5", "TypeScript"));

        foreach (var group in _allItems.Where(x => !string.IsNullOrWhiteSpace(x.Repository.PrimaryLanguage)).GroupBy(x => x.Repository.PrimaryLanguage, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Count()).Take(10))
            content.Add(new($"language:{group.Key.ToLowerInvariant()}", MetroTileKind.Language, group.Key, $"{group.Count()} 个关联仓库", "点击筛选", group.Key));

        foreach (var group in _allItems.SelectMany(x => x.Repository.Topics.Select(topic => (Topic: topic, Item: x))).Where(x => !string.IsNullOrWhiteSpace(x.Topic)).GroupBy(x => x.Topic, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Count()).Take(10))
            content.Add(new($"stack:{group.Key.ToLowerInvariant()}", MetroTileKind.Technology, group.Key, $"关联 {group.Select(x => x.Item.Id).Distinct().Count()} 个项目", "技术栈", group.Key));

        foreach (var item in _allItems)
        {
            var kind = item.Repository.Stars >= 100_000 ? MetroTileKind.FeaturedRepository : MetroTileKind.Repository;
            content.Add(new($"repository:{item.Repository.Id}", kind, item.Repository.FullName, item.Repository.Description, item.ReasonText, item.Repository.PrimaryLanguage, item.Repository.Id, item.Repository.OwnerAvatarUrl));
        }

        var catalog = _tips.GetTips(DateOnly.FromDateTime(DateTime.Today));
        for (var index = 0; index < 42; index++)
        {
            var tip = catalog[index % catalog.Count];
            var sizeMarker = tip.ColumnSpan == 2 && tip.RowSpan == 2 ? ":large:" : tip.ColumnSpan == 2 ? ":wide:" : ":small:";
            content.Add(new($"tip{sizeMarker}{index}:{tip.Key}", MetroTileKind.Tip, tip.Title, tip.Body, string.Join(" · ", new[] { tip.Category, tip.Attribution }.Where(x => !string.IsNullOrWhiteSpace(x))), tip.AccentKey, IsPlaceholder: true));
        }

        var scope = _session.Current.User?.GitHubId ?? "guest";
        _tileBoard = await _tileLayout.SynchronizeAsync(scope, SelectedSource.Source, content, minimumColumns, minimumRows);
        var repositoriesById = _allItems.GroupBy(x => x.Repository.Id).ToDictionary(x => x.Key, x => x.First());
        var rankingsByKey = TopLists.ToDictionary(x => $"ranking:{x.Title}", StringComparer.Ordinal);
        Tiles.Clear();
        foreach (var placement in _tileBoard.Placements)
        {
            repositoriesById.TryGetValue(placement.Content.RepositoryId ?? -1, out var repository);
            rankingsByKey.TryGetValue(placement.Content.Key, out var ranking);
            var tile = new MetroTileViewModel(placement, _tilePalette.Create(placement.Content.AccentKey), repository, ranking);
            Tiles.Add(tile);
            if (tile.IsRepository && !string.IsNullOrWhiteSpace(placement.Content.ImageUrl)) _ = LoadTileImageAsync(tile, placement.Content.ImageUrl);
        }
        TileCanvasWidth = Math.Max(1, _tileBoard.ExtentColumns * 100 - 4);
        TileCanvasHeight = Math.Max(1, _tileBoard.ExtentRows * 100 - 4);
        TileViewportX = _tileBoard.ViewportX; TileViewportY = _tileBoard.ViewportY;
        ApplyTileFilter();
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
    private UserPreference? _preferences;
    private bool _isLoaded;
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

    public string ThresholdText => $"匹配度达到 {NotificationThreshold:P0} 时提醒";
    public SettingsViewModel(IUserService users, ICacheService cache, IMemoryCacheStore memoryCache, IDbContextFactory<RepoGalaxyDbContext> databaseFactory, DatabaseLifecycleService databaseLifecycle, DiscoverySyncService syncService, IMetroTileLayoutService tileLayout, IAuthenticationSessionService session) { _users = users; _cache = cache; _memoryCache = memoryCache; _databaseFactory = databaseFactory; _databaseLifecycle = databaseLifecycle; _syncService = syncService; _tileLayout = tileLayout; _session = session; _ = LoadAsync(); }
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
    private async Task ClearCacheAsync()
    {
        await _memoryCache.ClearAsync();
        var removed = await _cache.PruneAsync(0);
        CacheStatus = $"已清理 {removed / 1024d / 1024d:N1} MB 可重建缓存";
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
        TileLayoutStatus = "首页 Tile 布局已重置，返回发现页后将创建新的稳定画布。";
    }
}

public sealed record GitIdentityAliasItem(long Id, string Name, string Email)
{
    public string Display => string.Join(" · ", new[] { Name, Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
public sealed record SyncIntervalOption(int Minutes, string Label);
