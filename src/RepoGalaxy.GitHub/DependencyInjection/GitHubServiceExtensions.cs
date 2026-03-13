using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepoGalaxy.GitHub.Auth;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.Configuration;
using RepoGalaxy.GitHub.Services;

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
        
        // 注册限流器
        services.AddSingleton<RateLimiter>(sp =>
        {
            var options = sp.GetRequiredService<GitHubOptions>();
            return new RateLimiter(options.RateLimitPerSecond);
        });
        
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
            var logger = sp.GetService<ILogger<OAuthCodeFlowService>>();
            return new OAuthCodeFlowService(options, logger);
        });
        
        // 注册 API 客户端
        services.AddSingleton<GitHubApiClient>(sp =>
        {
            var client = new GitHubApiClient();
            
            // 如果有存储的 Token，自动设置
            var tokenManager = sp.GetService<GitHubTokenManager>();
            if (tokenManager != null)
            {
                var token = tokenManager.GetTokenAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(token))
                {
                    client.SetAccessToken(token);
                }
            }
            
            return client;
        });
        
        // 注册同步服务
        services.AddScoped<RepositorySyncService>();
        
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
