using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.ViewModels.Dialogs;
using RepoGalaxy.Desktop.Views.Dialogs;
using RepoGalaxy.GitHub.Auth;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.Configuration;
using RepoGalaxy.GitHub.Services;
using RepoGalaxy.Recommendation.Services;

namespace RepoGalaxy.Desktop.ViewModels;

/// <summary>
/// MainWindow ViewModel - Fluent UI 2 风格
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly GitHubTokenManager _tokenManager;
    private readonly GitHubApiClient _apiClient;
    private readonly RepositorySyncService _syncService;
    private readonly BackgroundSyncService _backgroundSync;

    // 子视图模型
    public ExploreViewModel ExploreViewModel { get; }
    public BookmarksViewModel BookmarksViewModel { get; }
    public MyReposViewModel MyReposViewModel { get; }
    public LocalReposViewModel LocalReposViewModel { get; }

    // 导航状态
    [ObservableProperty] private string _currentViewName = "Explore";
    [ObservableProperty] private ViewModelBase _currentView;

    // 用户状态
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _currentUserLogin = string.Empty;
    [ObservableProperty] private string _currentUserAvatar = string.Empty;
    [ObservableProperty] private bool _isSyncing;
    [ObservableProperty] private string _syncStatus = string.Empty;

    // 从用户名获取首字母
    public string CurrentUserInitials => 
        string.IsNullOrEmpty(CurrentUserLogin) ? "?" : CurrentUserLogin[..Math.Min(2, CurrentUserLogin.Length)].ToUpper();

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        GitHubTokenManager tokenManager,
        GitHubApiClient apiClient,
        RepositorySyncService syncService,
        BackgroundSyncService backgroundSync,
        ExploreViewModel exploreVm,
        BookmarksViewModel bookmarksVm,
        MyReposViewModel myReposVm,
        LocalReposViewModel localReposVm)
    {
        _logger = logger;
        _tokenManager = tokenManager;
        _apiClient = apiClient;
        _syncService = syncService;
        _backgroundSync = backgroundSync;

        ExploreViewModel = exploreVm;
        BookmarksViewModel = bookmarksVm;
        MyReposViewModel = myReposVm;
        LocalReposViewModel = localReposVm;

        _currentView = exploreVm;

        // 初始化时检查登录状态
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // 检查是否有保存的Token
            var token = await _tokenManager.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                // 检查 Token 过期状态
                var tokenStatus = await _tokenManager.GetTokenStatusAsync();
                
                if (!tokenStatus.IsValid)
                {
                    _logger.LogWarning("Token 已过期，需要重新登录");
                    SyncStatus = "登录已过期，请重新登录";
                    await _tokenManager.ClearTokenAsync();
                    return;
                }
                
                // Token 即将过期警告
                if (tokenStatus.ExpiresIn.HasValue && tokenStatus.ExpiresIn.Value < TimeSpan.FromMinutes(30))
                {
                    _logger.LogWarning("Token 即将过期: {Message}", tokenStatus.Message);
                    SyncStatus = $"⚠️ {tokenStatus.Message}";
                }
                
                _apiClient.SetAccessToken(token);
                await LoadCurrentUserAsync();
                
                // 启动后台同步服务
                _backgroundSync.Start();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化失败");
        }
    }

    [RelayCommand]
    private void Navigate(string viewName)
    {
        CurrentViewName = viewName;
        CurrentView = viewName switch
        {
            "Explore" => ExploreViewModel,
            "Bookmarks" => BookmarksViewModel,
            "MyRepos" => MyReposViewModel,
            "Local" => LocalReposViewModel,
            _ => ExploreViewModel
        };

        // 加载对应视图数据
        _ = LoadViewDataAsync(viewName);
    }

    private async Task LoadViewDataAsync(string viewName)
    {
        try
        {
            switch (viewName)
            {
                case "Explore":
                    await ExploreViewModel.RefreshAsync();
                    break;
                case "Bookmarks":
                    await BookmarksViewModel.LoadBookmarksAsync();
                    break;
                case "MyRepos":
                    if (IsAuthenticated)
                        await MyReposViewModel.LoadRepositoriesAsync();
                    break;
                case "Local":
                    await LocalReposViewModel.LoadLocalRepositoriesAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载视图数据失败: {ViewName}", viewName);
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            // 获取服务
            var authService = App.Services?.GetService(typeof(GitHubAuthService)) as GitHubAuthService;
            
            if (authService == null)
            {
                SyncStatus = "认证服务未初始化";
                return;
            }
            
            var viewModel = new LoginDialogViewModel(authService, _tokenManager);
            
            var dialog = new LoginDialog { DataContext = viewModel };

            // 订阅事件关闭对话框
            viewModel.LoginSuccess += (_, _) => dialog.Close(true);
            viewModel.Cancelled += (_, _) => dialog.Close(false);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var result = await dialog.ShowDialog<bool>(desktop.MainWindow);
                if (result)
                {
                    // 从 TokenManager 获取 token 并设置到 API 客户端
                    var token = await _tokenManager.GetTokenAsync();
                    if (!string.IsNullOrEmpty(token))
                    {
                        _apiClient.SetAccessToken(token);
                    }
                    
                    await LoadCurrentUserAsync();
                    
                    // 启动后台同步
                    _backgroundSync.Start();
                    
                    await LoadViewDataAsync(CurrentViewName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录失败");
            SyncStatus = $"登录失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _tokenManager.ClearTokenAsync();
            _apiClient.ClearAccessToken();
            
            IsAuthenticated = false;
            CurrentUserLogin = string.Empty;
            CurrentUserAvatar = string.Empty;
            SyncStatus = "已退出登录";

            // 刷新视图
            await LoadViewDataAsync(CurrentViewName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "退出登录失败");
        }
    }

    [RelayCommand]
    private async Task SyncDataAsync()
    {
        if (!IsAuthenticated || IsSyncing) return;

        try
        {
            IsSyncing = true;
            SyncStatus = "正在同步数据...";

            // 使用后台同步服务执行完整同步
            var result = await _backgroundSync.PerformFullSyncAsync();
            
            SyncStatus = result.Status == Recommendation.Services.SyncStatus.Success 
                ? $"同步完成: +{result.UserReposAdded} 仓库" 
                : $"同步部分完成: {result.Message}";
            
            // 刷新当前视图
            await LoadViewDataAsync(CurrentViewName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步失败");
            SyncStatus = $"同步失败: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        // TODO: 打开设置对话框
        SyncStatus = "设置功能开发中...";
    }

    private async Task LoadCurrentUserAsync()
    {
        try
        {
            var user = await _apiClient.GetCurrentUserAsync();
            if (user != null)
            {
                CurrentUserLogin = user.Login;
                CurrentUserAvatar = user.AvatarUrl;
                IsAuthenticated = true;
                SyncStatus = $"已登录: {user.Login}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户信息失败");
            IsAuthenticated = false;
            CurrentUserLogin = string.Empty;
            CurrentUserAvatar = string.Empty;
        }
    }
}
