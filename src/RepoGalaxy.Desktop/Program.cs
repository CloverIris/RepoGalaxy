using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
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
using Serilog;
using System;
using System.IO;
using Serilog.Events;

namespace RepoGalaxy.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 配置日志
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
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

        // 数据库上下文只通过 factory 创建短生命周期实例，避免跨线程共享跟踪状态。
        services.AddDbContextFactory<RepoGalaxyDbContext>(options => options.UseSqlite($"Data Source={GetDatabasePath()};Cache=Shared;Pooling=True;Foreign Keys=True;Default Timeout=5"));
        services.AddSingleton(sp => new DatabaseLifecycleService(sp.GetRequiredService<IDbContextFactory<RepoGalaxyDbContext>>(), GetDatabasePath()));
        services.AddSingleton<IApplicationStartupCoordinator, ApplicationStartupCoordinator>();

        // 安全存储
        services.AddSingleton<ISecureStorage, SecureStorage>();

        // 数据服务
        services.AddSingleton<IRepositoryService, RepositoryService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<RepositoryService>();
        services.AddSingleton<DiscoveryStore>();
        services.AddSingleton<IMemoryCacheStore, MemoryCacheStore>();
        services.AddSingleton<IPersistentCacheStore, PersistentCacheStore>();
        services.AddSingleton<ICacheService, LayeredCacheService>();
        services.AddSingleton<ILazyRefreshCoordinator, LazyRefreshCoordinator>();
        services.AddSingleton<IMetroTileLayoutService, MetroTileLayoutService>();
        services.AddSingleton<ISemanticMosaicLayoutService, SemanticMosaicLayoutService>();
        services.AddSingleton<ISemanticIndexCatalogService, SemanticIndexCatalogService>();
        services.AddSingleton<ISpatialTileSearchService, SpatialTileSearchService>();
        services.AddSingleton<IVirtualTileWorldService, VirtualTileWorldService>();
        services.AddSingleton<ITileWorldPresentationService, TileWorldPresentationService>();
        services.AddSingleton<IRepositoryTileActionService, RepositoryTileActionService>();
        services.AddSingleton<IZoomableTileLayoutService, ZoomableTileLayoutService>();
        services.AddSingleton<IDetailPortalCoordinator, DetailPortalCoordinator>();
        services.AddSingleton<ITilePaletteService, TilePaletteService>();
        services.AddSingleton<ITipCatalog, TipCatalog>();
        services.AddSingleton<ITileImageService, TileImageService>();
        services.AddSingleton<IProfileImageService, ProfileImageService>();
        services.AddSingleton<IDetailContentService, DetailContentService>();
        services.AddSingleton<IExternalMetadataExtractor, ExternalMetadataExtractor>();
        services.AddSingleton<IMarkdownDocumentService, MarkdownDocumentService>();
        services.AddSingleton<ISafeMarkdownImageService, SafeMarkdownImageService>();
        services.AddSingleton<ILocalIdeDiscoveryService, LocalIdeDiscoveryService>();
        services.AddSingleton<ILocalRepositoryResolver, LocalRepositoryResolver>();
        services.AddSingleton<IRepositoryCloneService, RepositoryCloneService>();
        services.AddSingleton<IIdeLauncher, IdeLauncher>();
        services.AddSingleton<IIdePreferenceService, IdePreferenceService>();
        services.AddHttpClient("external-metadata").ConfigurePrimaryHttpMessageHandler(ExternalMetadataSecurity.CreateHandler);
        services.AddHttpClient("markdown-images").ConfigurePrimaryHttpMessageHandler(ExternalMetadataSecurity.CreateHandler);

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
            options.ClientSecret = Environment.GetEnvironmentVariable("REPOGALAXY_GITHUB_CLIENT_SECRET")
                ?? Environment.GetEnvironmentVariable("REP0GALAXY_GITHUB_CLIENT_SECRET");
            options.Scope = "repo read:user";
            options.TimeoutSeconds = 30;
            options.RateLimitPerSecond = 10;
        });
        
        // 单独注册接口映射
        services.AddSingleton<IGitHubClient>(sp => sp.GetRequiredService<GitHubApiClient>());

        // 推荐引擎
        services.AddSingleton<IRankingPipeline, RankingPipeline>();
        services.AddSingleton<IRankingConfigurationService, RankingConfigurationService>();
        services.AddSingleton<RecommendationEngine>();
        services.AddSingleton<IRecommendationEngine>(sp => sp.GetRequiredService<RecommendationEngine>());
        services.AddSingleton<IRankingRebuildService>(sp => sp.GetRequiredService<RecommendationEngine>());
        
        // 数据源服务
        services.AddSingleton<DiscoverySyncService>();
        
        // 后台同步服务
        
        // 聚类管理器 (拖拽摇晃聚类)
        
        services.AddSingleton<INotificationService>(_ => new ToastNotificationService(null));
        services.AddSingleton<IDesktopNotificationService, DesktopNotificationService>();
        services.AddSingleton<IAuthenticationAuditService, AuthenticationAuditService>();
        services.AddSingleton<IAuthenticationSessionService, AuthenticationSessionService>();
        services.AddSingleton<IExternalLinkService, ExternalLinkService>();
        services.AddSingleton<DashboardDataService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<RepositoryDetailsViewModel>();
        services.AddSingleton<DashboardRailViewModel>();
        services.AddSingleton<AccountProfileViewModel>();
        services.AddSingleton<DiscoverViewModel>();
        services.AddSingleton<SubscriptionsViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<NotificationsViewModel>();
        services.AddSingleton<SettingsViewModel>();
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
