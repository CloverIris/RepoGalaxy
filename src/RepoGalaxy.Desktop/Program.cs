using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.GitHub.Auth;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.DependencyInjection;
using RepoGalaxy.GitHub.Services;
using RepoGalaxy.Recommendation.Engine;
using RepoGalaxy.Recommendation.Services;
using RepoGalaxy.Desktop.Controls;
using Serilog;
using System;
using System.IO;

namespace RepoGalaxy.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 配置日志
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                path: GetLogFilePath(),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("RepoGalaxy 启动中...");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>(() => new App(CreateServiceProvider()))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    /// <summary>
    /// 创建 DI 服务容器
    /// </summary>
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // 添加日志服务
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        // 数据库上下文
        services.AddSingleton<RepoGalaxyDbContext>(sp =>
        {
            var dbPath = GetDatabasePath();
            Log.Information("数据库路径: {DbPath}", dbPath);
            return new RepoGalaxyDbContext(dbPath);
        });

        // 安全存储
        services.AddSingleton<ISecureStorage, SecureStorage>();

        // 数据服务
        services.AddSingleton<IRepositoryService, RepositoryService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<RepositoryService>();

        // GitHub 服务 - 使用扩展方法注册
        services.AddGitHubServices(options =>
        {
            // GitHub OAuth App 配置
            //
            // ┌─────────────────────────────────────────────────────────────────┐
            // │  开发者须知:                                                      │
            // │  需要在 GitHub 注册 OAuth App:                                     │
            // │  https://github.com/settings/applications/new                    │
            // │                                                                  │
            // │  填写信息:                                                        │
            // │  - Application name: RepoGalaxy                                  │
            // │  - Homepage URL: http://localhost:5000                           │
            // │  - Authorization callback URL: http://localhost:5000/callback    │
            // │                                                                  │
            // │  重要：在 OAuth App 设置页面勾选 "Enable Device Flow"             │
            // │                                                                  │
            // │  获取 Client ID，填入下方（Device Flow 不需要 Client Secret）      │
            // └─────────────────────────────────────────────────────────────────┘
            
            // 1. 从环境变量读取 Client ID
            var envClientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID");
            
            // 2. 硬编码默认值（开发测试用）
            const string DefaultClientId = "Ov23lis0Gybr3Ddzp8R2";
            
            // 设置 Client ID
            if (!string.IsNullOrEmpty(envClientId) && envClientId != "YOUR_CLIENT_ID")
            {
                options.ClientId = envClientId;
                Log.Information("使用环境变量 GITHUB_CLIENT_ID");
            }
            else if (!string.IsNullOrEmpty(DefaultClientId) && DefaultClientId != "Ov23liXXXXXXXXXXXXXXXXX")
            {
                options.ClientId = DefaultClientId;
                Log.Information("使用默认 GitHub OAuth Client ID（Device Flow 模式）");
            }
            else
            {
                options.ClientId = "UNCONFIGURED";
                Log.Warning("GitHub OAuth Client ID 未配置，Device Flow 将不可用。请使用 Personal Access Token 登录。");
            }
            
            // Device Flow 不需要 Client Secret
            options.ClientSecret = null;
            options.Scope = "repo read:user";
            options.TimeoutSeconds = 30;
            options.RateLimitPerSecond = 10;
        });
        
        // 单独注册接口映射
        services.AddSingleton<IGitHubClient>(sp => sp.GetRequiredService<GitHubApiClient>());

        // 推荐引擎
        services.AddSingleton<IRecommendationEngine, RecommendationEngine>();
        
        // 数据源服务
        services.AddSingleton<DataSourceService>();
        
        // 后台同步服务
        services.AddSingleton<BackgroundSyncService>();
        
        // 聚类管理器 (拖拽摇晃聚类)
        services.AddScoped<ClusterManager>();
        
        // 注册 RepositorySyncService
        services.AddScoped<RepositorySyncService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<ExploreViewModel>();
        services.AddSingleton<BookmarksViewModel>();
        services.AddSingleton<MyReposViewModel>();
        services.AddSingleton<LocalReposViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 获取数据库路径
    /// </summary>
    private static string GetDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "RepoGalaxy");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "repogalaxy.db");
    }

    /// <summary>
    /// 获取日志文件路径
    /// </summary>
    private static string GetLogFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logFolder = Path.Combine(appData, "RepoGalaxy", "Logs");
        Directory.CreateDirectory(logFolder);
        return Path.Combine(logFolder, "repogalaxy-.log");
    }
}
