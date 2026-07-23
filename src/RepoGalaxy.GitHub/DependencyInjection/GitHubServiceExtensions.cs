using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepoGalaxy.GitHub.Auth;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.Configuration;
using RepoGalaxy.GitHub.Services;
using RepoGalaxy.Core.Interfaces;

namespace RepoGalaxy.GitHub.DependencyInjection;

/// <summary>
/// GitHub 服务依赖注入扩展
/// </summary>
public static class GitHubServiceExtensions
{
    /// <summary>
    /// 添加 GitHub 相关服务
    /// </summary>
    public static IServiceCollection AddGitHubServices(
        this IServiceCollection services, 
        Action<GitHubOptions> configureOptions)
    {
        // 配置选项
        services.AddSingleton(sp =>
        {
            var options = new GitHubOptions();
            configureOptions(options);
            return options;
        });
        
        services.AddSingleton<GitHubRequestBudget>();
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
        
        // 注册 Token 管理器
        services.AddSingleton<GitHubTokenManager>();
        
        // 注册认证服务
        services.AddSingleton<GitHubAuthService>(sp =>
        {
            var options = sp.GetRequiredService<GitHubOptions>();
            var logger = sp.GetService<ILogger<GitHubAuthService>>();
            return new GitHubAuthService(options, logger: logger);
        });
        
        // 注册 OAuth Code Flow 服务
        services.AddSingleton<OAuthCodeFlowService>(sp =>
        {
            var options = sp.GetRequiredService<GitHubOptions>();
            return new OAuthCodeFlowService(options);
        });
        
        services.AddHttpClient("RepoGalaxy.GitHub", (sp, client) =>
        {
            var options = sp.GetRequiredService<GitHubOptions>();
            client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RepoGalaxy/3.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        });
        services.AddSingleton<GitHubApiClient>(sp => new(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("RepoGalaxy.GitHub"),
            sp.GetRequiredService<GitHubRequestBudget>(),
            sp.GetRequiredService<ISyncOrchestrator>(),
            sp.GetService<ICacheService>(),
            sp.GetService<IUserService>(),
            sp.GetService<IApiRequestTelemetry>()));
        
        return services;
    }
    
    /// <summary>
    /// 添加 GitHub 服务（使用默认配置）
    /// </summary>
    public static IServiceCollection AddGitHubServices(this IServiceCollection services, string clientId)
    {
        return services.AddGitHubServices(options =>
        {
            options.ClientId = clientId;
        });
    }
}
