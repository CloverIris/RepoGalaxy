using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Desktop.ViewModels.Dialogs;
using RepoGalaxy.Desktop.Views.Dialogs;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.GitHub.Auth;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.Services;
using RepoGalaxy.Recommendation.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GitHubTokenManager _tokens;
    private readonly GitHubApiClient _github;
    private readonly GitHubAuthService _auth;
    private readonly DiscoverySyncService _sync;
    private readonly IDesktopNotificationService _desktopNotifications;
    public DiscoverViewModel Discover { get; }
    public SubscriptionsViewModel Subscriptions { get; }
    public LibraryViewModel Library { get; }
    public NotificationsViewModel Notifications { get; }
    public MyReposViewModel MyRepos { get; }
    public LocalReposViewModel LocalRepos { get; }
    public SettingsViewModel Settings { get; }
    [ObservableProperty] private ViewModelBase _currentView;
    [ObservableProperty] private string _currentViewName = "Discover";
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _currentUserLogin = string.Empty;
    [ObservableProperty] private string _syncStatus = "准备就绪";

    public MainWindowViewModel(GitHubTokenManager tokens, GitHubApiClient github, GitHubAuthService auth, DiscoverySyncService sync, IDesktopNotificationService desktopNotifications, DiscoverViewModel discover, SubscriptionsViewModel subscriptions, LibraryViewModel library, NotificationsViewModel notifications, MyReposViewModel myRepos, LocalReposViewModel localRepos, SettingsViewModel settings)
    {
        _tokens = tokens; _github = github; _auth = auth; _sync = sync; _desktopNotifications = desktopNotifications;
        Discover = discover; Subscriptions = subscriptions; Library = library; Notifications = notifications; MyRepos = myRepos; LocalRepos = localRepos; Settings = settings;
        _currentView = discover;
        _sync.NewFeedItem += (_, item) => { SyncStatus = $"发现新内容：{item.Repository.FullName}"; };
        _sync.NewFeedItem += (_, item) => _desktopNotifications.ShowFeedNotification(item);
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var token = await _tokens.GetTokenAsync();
        if (!string.IsNullOrEmpty(token)) { _github.SetAccessToken(token); var user = await _github.GetCurrentUserAsync(); IsAuthenticated = user != null; CurrentUserLogin = user?.Login ?? string.Empty; _sync.Start(); }
        await Discover.LoadAsync();
    }

    [RelayCommand] private async Task NavigateAsync(string viewName)
    {
        CurrentViewName = viewName;
        CurrentView = viewName switch { "Subscriptions" => Subscriptions, "Library" => Library, "Notifications" => Notifications, "MyRepos" => MyRepos, "LocalRepos" => LocalRepos, "Settings" => Settings, _ => Discover };
        switch (CurrentView) { case DiscoverViewModel vm: await vm.LoadAsync(); break; case SubscriptionsViewModel vm: await vm.LoadAsync(); break; case LibraryViewModel vm: await vm.LoadAsync(); break; case NotificationsViewModel vm: await vm.LoadAsync(); break; case MyReposViewModel vm: if (IsAuthenticated) await vm.LoadRepositoriesAsync(); break; case LocalReposViewModel vm: await vm.LoadLocalRepositoriesAsync(); break; }
    }

    [RelayCommand] private async Task LoginAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null) return;
        var vm = new LoginDialogViewModel(_auth, _tokens); var dialog = new LoginDialog { DataContext = vm };
        vm.LoginSuccess += (_, _) => dialog.Close(true); vm.Cancelled += (_, _) => dialog.Close(false);
        if (await dialog.ShowDialog<bool>(desktop.MainWindow)) { await InitializeAsync(); SyncStatus = "登录成功，正在同步发现内容"; }
    }

    [RelayCommand] private async Task LogoutAsync() { await _tokens.ClearTokenAsync(); _github.ClearAccessToken(); IsAuthenticated = false; CurrentUserLogin = string.Empty; SyncStatus = "已退出登录"; }
    [RelayCommand] private async Task SyncAsync() { await Discover.SyncAsync(); SyncStatus = "同步完成"; }
}
