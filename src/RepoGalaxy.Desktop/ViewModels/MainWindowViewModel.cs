using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
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
    private readonly IAuthenticationSessionService _session;
    private readonly GitHubApiClient _github;
    private readonly GitHubAuthService _deviceFlow;
    private readonly OAuthCodeFlowService _loopback;
    private readonly DiscoverySyncService _sync;
    private readonly IDesktopNotificationService _notifications;
    private readonly IAuthenticationAuditService _audit;
    private readonly GitHubRequestBudget _budget;

    public DiscoverViewModel Discover { get; }
    public SubscriptionsViewModel Subscriptions { get; }
    public LibraryViewModel Library { get; }
    public NotificationsViewModel Notifications { get; }
    public MyReposViewModel MyRepos { get; }
    public LocalReposViewModel LocalRepos { get; }
    public SettingsViewModel Settings { get; }
    public RepositoryDetailsViewModel Details { get; }
    public DashboardRailViewModel DashboardRail { get; }
    public AccountProfileViewModel AccountProfile { get; }
    public ViewModelBase RightRailContent => Details.IsOpen ? Details : DashboardRail;
    private bool _isDashboardRailInline = true;
    private bool _isDashboardRailRequested;
    public bool IsRightRailOpen => !Discover.ShouldSuppressRightRail && (Details.IsOpen || ReferenceEquals(CurrentView, Discover) && (_isDashboardRailInline || _isDashboardRailRequested));
    public bool CanToggleRightRail => ReferenceEquals(CurrentView, Discover) && !_isDashboardRailInline;

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
    [ObservableProperty] private double _coreBudgetRatio;
    [ObservableProperty] private double _searchBudgetRatio;
    [ObservableProperty] private string _coreBudgetText = "Core · 等待首次请求";
    [ObservableProperty] private string _searchBudgetText = "Search · 等待首次请求";
    [ObservableProperty] private string _budgetSessionText = "游客额度";

    public string AccountLabel => IsAuthenticated ? CurrentUserLogin : "登录 GitHub";
    public string AccountInitial => string.IsNullOrWhiteSpace(CurrentUserLogin) ? "G" : CurrentUserLogin[..1].ToUpperInvariant();
    public string CoreBudgetColor => BudgetColor(CoreBudgetRatio);
    public string SearchBudgetColor => BudgetColor(SearchBudgetRatio);

    public MainWindowViewModel(
        IAuthenticationSessionService session,
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
        RepositoryDetailsViewModel details,
        DashboardRailViewModel dashboardRail,
        AccountProfileViewModel accountProfile,
        GitHubRequestBudget budget)
    {
        _session = session;
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
        DashboardRail = dashboardRail;
        AccountProfile = accountProfile;
        _budget = budget;
        _budget.Changed += (_, snapshot) => Dispatcher.UIThread.Post(() => ApplyBudget(snapshot));
        ApplyBudget(_budget.Snapshot);
        Discover.ImmersiveDetailChanged += (_, _) => OnPropertyChanged(nameof(IsRightRailOpen));
        _currentView = discover;
        Details.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RepositoryDetailsViewModel.IsOpen))
            {
                OnPropertyChanged(nameof(RightRailContent));
                OnPropertyChanged(nameof(IsRightRailOpen));
            }
        };

        PrimaryNavigation =
        [
            new("Discover", "发现", "\uE721", "M3,5 L9,3 L7,9 L5,10 L3,16 L9,14 L11,8 L16,6 L14,12 L8,14"),
            new("Subscriptions", "订阅", "\uE8A5", "M4,3 L16,3 L16,15 L11,12 L6,15 L6,5 L4,5 Z"),
            new("Library", "收藏库", "\uE8F1", "M3,4 L9,4 L11,6 L17,6 L17,16 L3,16 Z"),
            new("Notifications", "通知", "\uE7F4", "M10,2 C7,2 5,4 5,7 L5,11 L3,14 L17,14 L15,11 L15,7 C15,4 13,2 10,2 M8,16 L12,16 C12,18 8,18 8,16")
        ];
        WorkspaceNavigation =
        [
            new("MyRepos", "我的仓库", "\uE8B7", "M4,5 L8,5 L10,7 L16,7 L16,15 L4,15 Z M7,9 L13,9 M7,12 L11,12", "Workspace"),
            new("LocalRepos", "本地仓库", "\uE838", "M3,4 L17,4 L17,15 L3,15 Z M6,8 L8,10 L6,12 M10,12 L14,12", "Workspace")
        ];
        SettingsNavigation = new("Settings", "设置", "\uE713", "M10,2 L12,3 L14,2 L16,4 L15,6 L16,8 L18,9 L18,11 L16,12 L15,14 L16,16 L14,18 L12,17 L10,18 L8,17 L6,18 L4,16 L5,14 L4,12 L2,11 L2,9 L4,8 L5,6 L4,4 L6,2 L8,3 Z M10,7 A3,3 0 1 0 10,13 A3,3 0 1 0 10,7", "Footer");
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
    }

    private bool _hasInitialized;
    public async Task InitializeAsync()
    {
        if (_hasInitialized) return;
        _hasInitialized = true;
        try
        {
            // Render the local snapshot before any credential validation or
            // remote /user request. The shell stays interactive throughout.
            await Task.Yield();
            await Discover.LoadAsync();
            await DashboardRail.LoadAsync();

            ConnectionStatus = "正在验证本地会话";
            var session = await _session.RestoreAsync();
            IsAuthenticated = session.IsAuthenticated;
            CurrentUserLogin = session.User?.Login ?? string.Empty;
            await AccountProfile.SetUserAsync(session.User);
            ConnectionStatus = session.IsAuthenticated ? "GitHub 已连接" : "游客模式 · 使用本地缓存";
            if (session.IsAuthenticated)
                await Discover.LoadAsync();
        }
        catch
        {
            _hasInitialized = false;
            throw;
        }
    }

    [RelayCommand]
    private async Task NavigateAsync(NavigationItemViewModel item)
    {
        if (ReferenceEquals(CurrentView, Discover) && item.Key != "Discover") await Discover.DeactivateAsync();
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
        OnPropertyChanged(nameof(IsRightRailOpen));
        OnPropertyChanged(nameof(CanToggleRightRail));
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
    private void ToggleRightRail()
    {
        if (_isDashboardRailInline) return;
        _isDashboardRailRequested = !_isDashboardRailRequested;
        OnPropertyChanged(nameof(IsRightRailOpen));
        OnPropertyChanged(nameof(CanToggleRightRail));
    }

    public void SetDashboardRailInline(bool value)
    {
        _isDashboardRailInline = value;
        if (value) _isDashboardRailRequested = false;
        OnPropertyChanged(nameof(IsRightRailOpen));
        OnPropertyChanged(nameof(CanToggleRightRail));
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null) return;
        var vm = new LoginDialogViewModel(_deviceFlow, _loopback, _session, _audit);
        var dialog = new LoginDialog { DataContext = vm };
        vm.LoginSuccess += (_, _) => dialog.Close(true);
        vm.Cancelled += (_, _) => dialog.Close(false);
        if (await dialog.ShowDialog<bool>(desktop.MainWindow))
        {
            var session = _session.Current;
            IsAuthenticated = session.IsAuthenticated;
            CurrentUserLogin = session.User?.Login ?? string.Empty;
            await AccountProfile.SetUserAsync(session.User);
            ConnectionStatus = session.IsAuthenticated ? "GitHub 已连接" : "游客模式";
            await Discover.LoadAsync();
            await DashboardRail.LoadAsync();
            SyncStatus = "登录成功，已开始同步";
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _session.SignOutAsync();
        IsAuthenticated = false;
        CurrentUserLogin = string.Empty;
        await AccountProfile.SetUserAsync(null);
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

    [RelayCommand]
    private async Task OpenAccountProfileAsync() => await AccountProfile.EnsureSocialAccountsAsync();

    [RelayCommand]
    private async Task RefreshRateLimitAsync()
    {
        SyncStatus = "正在读取 GitHub API 额度…";
        var rate = await _github.GetRateLimitAsync();
        if (rate is not null) _budget.Update(rate);
        SyncStatus = rate is null ? "额度读取失败，保留最近观测值" : "API 额度已更新";
    }

    public void MoveToNextSearchMatch()
    {
        if (ReferenceEquals(CurrentView, Discover)) Discover.MoveToNextSearchMatch();
    }

    private void ApplyBudget(GitHubBudgetSnapshot snapshot)
    {
        BudgetSessionText = snapshot.SessionKind == GitHubBudgetSessionKind.Authenticated ? "登录额度" : "游客额度";
        CoreBudgetRatio = snapshot.Core?.UsedRatio ?? 0;
        SearchBudgetRatio = snapshot.Search?.UsedRatio ?? 0;
        CoreBudgetText = FormatBudget("Core", snapshot.Core);
        SearchBudgetText = FormatBudget("Search", snapshot.Search);
    }

    private static string FormatBudget(string name, GitHubRateWindow? window)
        => window is null
            ? $"{name} · 等待首次请求"
            : $"{name} · {window.EffectiveUsed:N0} / {window.Limit:N0} · {window.Remaining:N0} 剩余 · {window.ResetAt.LocalDateTime:HH:mm} 重置";

    private static string BudgetColor(double ratio) => ratio >= .95 ? "#E81123" : ratio >= .80 ? "#FFB900" : "#4C9AFF";

    partial void OnCoreBudgetRatioChanged(double value) => OnPropertyChanged(nameof(CoreBudgetColor));
    partial void OnSearchBudgetRatioChanged(double value) => OnPropertyChanged(nameof(SearchBudgetColor));

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
