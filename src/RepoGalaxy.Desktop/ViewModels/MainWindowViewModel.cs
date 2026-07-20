using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Desktop.ViewModels.Dialogs;
using RepoGalaxy.Desktop.Views.Dialogs;
using RepoGalaxy.GitHub.Auth;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.Services;
using RepoGalaxy.Recommendation.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly GitHubTokenManager _tokens;
    private readonly GitHubApiClient _github;
    private readonly GitHubAuthService _deviceFlow;
    private readonly OAuthCodeFlowService _loopback;
    private readonly DiscoverySyncService _sync;
    private readonly IDesktopNotificationService _notifications;
    private readonly IAuthenticationAuditService _audit;

    public DiscoverViewModel Discover { get; }
    public SubscriptionsViewModel Subscriptions { get; }
    public LibraryViewModel Library { get; }
    public NotificationsViewModel Notifications { get; }
    public MyReposViewModel MyRepos { get; }
    public LocalReposViewModel LocalRepos { get; }
    public SettingsViewModel Settings { get; }
    public RepositoryDetailsViewModel Details { get; }

    public IReadOnlyList<NavigationItemViewModel> PrimaryNavigation { get; }
    public IReadOnlyList<NavigationItemViewModel> WorkspaceNavigation { get; }
    public NavigationItemViewModel SettingsNavigation { get; }

    [ObservableProperty] private ViewModelBase _currentView;
    [ObservableProperty] private string _currentViewName = "发现";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _searchPlaceholder = "搜索当前 Feed";
    [ObservableProperty] private bool _canSearch = true;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _currentUserLogin = string.Empty;
    [ObservableProperty] private string _syncStatus = "正在读取本地内容";
    [ObservableProperty] private string _connectionStatus = "游客模式";
    [ObservableProperty] private bool _isNavigationOpen = true;

    public string AccountLabel => IsAuthenticated ? CurrentUserLogin : "登录 GitHub";
    public string AccountInitial => string.IsNullOrWhiteSpace(CurrentUserLogin) ? "G" : CurrentUserLogin[..1].ToUpperInvariant();

    public MainWindowViewModel(
        GitHubTokenManager tokens,
        GitHubApiClient github,
        GitHubAuthService deviceFlow,
        OAuthCodeFlowService loopback,
        DiscoverySyncService sync,
        IDesktopNotificationService notifications,
        IAuthenticationAuditService audit,
        DiscoverViewModel discover,
        SubscriptionsViewModel subscriptions,
        LibraryViewModel library,
        NotificationsViewModel notificationsView,
        MyReposViewModel myRepos,
        LocalReposViewModel localRepos,
        SettingsViewModel settings,
        RepositoryDetailsViewModel details)
    {
        _tokens = tokens;
        _github = github;
        _deviceFlow = deviceFlow;
        _loopback = loopback;
        _sync = sync;
        _notifications = notifications;
        _audit = audit;
        Discover = discover;
        Subscriptions = subscriptions;
        Library = library;
        Notifications = notificationsView;
        MyRepos = myRepos;
        LocalRepos = localRepos;
        Settings = settings;
        Details = details;
        _currentView = discover;

        PrimaryNavigation =
        [
            new("Discover", "发现", "M3,5 L9,3 L7,9 L5,10 L3,16 L9,14 L11,8 L16,6 L14,12 L8,14"),
            new("Subscriptions", "订阅", "M4,3 L16,3 L16,15 L11,12 L6,15 L6,5 L4,5 Z"),
            new("Library", "收藏库", "M3,4 L9,4 L11,6 L17,6 L17,16 L3,16 Z"),
            new("Notifications", "通知", "M10,2 C7,2 5,4 5,7 L5,11 L3,14 L17,14 L15,11 L15,7 C15,4 13,2 10,2 M8,16 L12,16 C12,18 8,18 8,16")
        ];
        WorkspaceNavigation =
        [
            new("MyRepos", "我的仓库", "M4,5 L8,5 L10,7 L16,7 L16,15 L4,15 Z M7,9 L13,9 M7,12 L11,12", "Workspace"),
            new("LocalRepos", "本地仓库", "M3,4 L17,4 L17,15 L3,15 Z M6,8 L8,10 L6,12 M10,12 L14,12", "Workspace")
        ];
        SettingsNavigation = new("Settings", "设置", "M10,2 L12,3 L14,2 L16,4 L15,6 L16,8 L18,9 L18,11 L16,12 L15,14 L16,16 L14,18 L12,17 L10,18 L8,17 L6,18 L4,16 L5,14 L4,12 L2,11 L2,9 L4,8 L5,6 L4,4 L6,2 L8,3 Z M10,7 A3,3 0 1 0 10,13 A3,3 0 1 0 10,7", "Footer");
        PrimaryNavigation[0].IsSelected = true;

        Discover.LoginRequested += async (_, _) => await LoginAsync();
        Discover.SubscriptionRequested += async (_, topic) =>
        {
            Subscriptions.PrefillTopic(topic);
            await NavigateAsync(PrimaryNavigation.First(x => x.Key == "Subscriptions"));
        };

        _sync.NewFeedItem += (_, item) =>
        {
            SyncStatus = $"发现了 {item.Repository.FullName}";
            _notifications.ShowFeedNotification(item);
        };
        _sync.StatusChanged += (_, status) => SyncStatus = status;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var token = await _tokens.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _github.SetAccessToken(token);
            var user = await _github.GetCurrentUserAsync();
            if (user is not null)
            {
                IsAuthenticated = true;
                CurrentUserLogin = user.Login;
                ConnectionStatus = "GitHub 已连接";
                await _tokens.SaveSessionMetadataAsync("已验证会话", user.Login, "repo read:user");
                _audit.Record("session", "verified", user.Login);
                _sync.Start(true);
                await Discover.LoadAsync();
                return;
            }

            await _tokens.ClearTokenAsync();
            _github.ClearAccessToken();
            _audit.Record("session", "invalid");
        }

        IsAuthenticated = false;
        ConnectionStatus = "游客模式 · 使用本地缓存";
        _sync.Start(false);
        await Discover.LoadAsync();
    }

    [RelayCommand]
    private async Task NavigateAsync(NavigationItemViewModel item)
    {
        foreach (var navigation in PrimaryNavigation.Concat(WorkspaceNavigation).Append(SettingsNavigation))
            navigation.IsSelected = navigation == item;

        Details.Close();
        SearchText = string.Empty;
        CurrentView = item.Key switch
        {
            "Subscriptions" => Subscriptions,
            "Library" => Library,
            "Notifications" => Notifications,
            "MyRepos" => MyRepos,
            "LocalRepos" => LocalRepos,
            "Settings" => Settings,
            _ => Discover
        };
        CurrentViewName = item.Title;
        ConfigureSearch(item.Key);

        switch (CurrentView)
        {
            case DiscoverViewModel vm: await vm.LoadAsync(); break;
            case SubscriptionsViewModel vm: await vm.LoadAsync(); break;
            case LibraryViewModel vm: await vm.LoadAsync(); break;
            case NotificationsViewModel vm: await vm.LoadAsync(); break;
            case MyReposViewModel vm when IsAuthenticated: await vm.LoadRepositoriesAsync(); break;
            case MyReposViewModel vm: vm.SetAuthenticationRequired(); break;
            case LocalReposViewModel vm: await vm.LoadLocalRepositoriesAsync(); break;
            case SettingsViewModel vm: await vm.LoadAsync(); break;
        }
    }

    [RelayCommand]
    private void ToggleNavigation() => IsNavigationOpen = !IsNavigationOpen;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null) return;
        var vm = new LoginDialogViewModel(_deviceFlow, _loopback, _tokens, _audit);
        var dialog = new LoginDialog { DataContext = vm };
        vm.LoginSuccess += (_, _) => dialog.Close(true);
        vm.Cancelled += (_, _) => dialog.Close(false);
        if (await dialog.ShowDialog<bool>(desktop.MainWindow))
        {
            await InitializeAsync();
            SyncStatus = "登录成功，已开始同步";
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _tokens.ClearTokenAsync();
        _github.ClearAccessToken();
        IsAuthenticated = false;
        CurrentUserLogin = string.Empty;
        ConnectionStatus = "游客模式 · 使用本地缓存";
        _audit.Record("session", "signed-out");
        SyncStatus = "已退出登录";
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        SyncStatus = "正在同步…";
        await Discover.SyncAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        if (CurrentView is ISearchablePage searchable) searchable.SearchText = value;
    }

    partial void OnIsAuthenticatedChanged(bool value)
    {
        OnPropertyChanged(nameof(AccountLabel));
        OnPropertyChanged(nameof(AccountInitial));
    }

    partial void OnCurrentUserLoginChanged(string value)
    {
        OnPropertyChanged(nameof(AccountLabel));
        OnPropertyChanged(nameof(AccountInitial));
    }

    private void ConfigureSearch(string key)
    {
        CanSearch = key != "Settings";
        SearchPlaceholder = key switch
        {
            "Subscriptions" => "搜索订阅规则",
            "Library" => "搜索收藏库",
            "Notifications" => "搜索通知",
            "MyRepos" => "搜索我的仓库",
            "LocalRepos" => "搜索本地仓库",
            "Settings" => "设置页无需搜索",
            _ => "搜索当前 Feed"
        };
    }
}
