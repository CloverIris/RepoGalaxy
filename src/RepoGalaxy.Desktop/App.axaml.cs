using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Desktop.Views;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Recommendation.Services;
using Serilog;
using System;

namespace RepoGalaxy.Desktop;

public partial class App : Application
{
    private readonly IServiceProvider? _serviceProvider;
    
    /// <summary>
    /// 全局服务访问器
    /// </summary>
    public static IServiceProvider? Services => ((App?)Current)?._serviceProvider;

    public App()
    {
    }

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 初始化数据库
        if (!InitializeDatabase(out var initializationError))
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime failedDesktop)
            {
                var status = new TextBlock { Text = initializationError, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 16 };
                var restore = new Button { Content = "从最近备份恢复", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
                restore.Click += async (_, _) =>
                {
                    restore.IsEnabled = false;
                    var lifecycle = _serviceProvider?.GetService<DatabaseLifecycleService>();
                    var restored = lifecycle is not null && await lifecycle.RestoreLatestBackupAsync();
                    status.Text = restored ? "数据库已恢复。请重新启动 RepoGalaxy。" : "没有可用的完整备份，恢复未执行。";
                    restore.IsVisible = !restored;
                };
                failedDesktop.MainWindow = new Window
                {
                    Title = "RepoGalaxy · 数据库恢复",
                    Width = 620,
                    Height = 280,
                    Content = new StackPanel { Margin = new Thickness(32), Spacing = 20, Children = { new TextBlock { Text = "本地数据需要修复", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.SemiBold }, status, restore } }
                };
            }
            base.OnFrameworkInitializationCompleted();
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 登录窗口等临时窗口不应延长应用生命周期；主窗口关闭即进入统一清理链路。
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var mainWindow = new MainWindow();
            ToastNotificationService.Attach(mainWindow);
            
            // 使用 DI 创建 ViewModel
            if (_serviceProvider != null)
            {
                var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                mainWindow.DataContext = viewModel;
            }

            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) =>
            {
                Log.Information("开始关闭 RepoGalaxy 后台服务");
                _serviceProvider?.GetService<DatabaseLifecycleService>()?.MarkCleanShutdown();
                Log.Information("数据库已标记为正常关闭，正在停止同步服务");
                _serviceProvider?.GetService<DiscoverySyncService>()?.StopAsync().GetAwaiter().GetResult();
                Log.Information("同步服务已停止，正在释放服务容器");
                (_serviceProvider as IDisposable)?.Dispose();
                Log.Information("RepoGalaxy 后台服务已全部停止");
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    private bool InitializeDatabase(out string error)
    {
        error = string.Empty;
        try
        {
            if (_serviceProvider != null)
            {
                var lifecycle = _serviceProvider.GetRequiredService<DatabaseLifecycleService>();
                var result = lifecycle.InitializeAsync().GetAwaiter().GetResult();
                if (!result.Success) { error = result.Message; return false; }
                using var dbContext = _serviceProvider.GetRequiredService<IDbContextFactory<RepoGalaxyDbContext>>().CreateDbContext();
                DatabaseSeeder.Seed(dbContext);
                try
                {
                    _serviceProvider.GetRequiredService<IRepositoryCloneService>().CleanupAbandonedAsync().GetAwaiter().GetResult();
                }
                catch (Exception cleanupError)
                {
                    // Abandoned clone cleanup is recoverable and must never make a
                    // healthy database look corrupt or prevent the main window opening.
                    Log.Warning(cleanupError, "未完成的克隆工作区清理失败，将在下次启动重试");
                }
                Log.Information("数据库初始化完成");
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库初始化失败");
            error = $"数据库初始化失败：{ex.Message}";
            return false;
        }
    }
}
