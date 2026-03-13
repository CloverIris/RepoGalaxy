namespace RepoGalaxy.GitHub.Configuration;

/// <summary>
/// GitHub 配置选项
/// </summary>
public class GitHubOptions
{
    /// <summary>
    /// GitHub OAuth App 的 Client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>
    /// GitHub OAuth App 的 Client Secret（可选，Device Flow 不需要）
    /// </summary>
    public string? ClientSecret { get; set; }
    
    /// <summary>
    /// OAuth 授权范围
    /// </summary>
    public string Scope { get; set; } = "repo read:user";
    
    /// <summary>
    /// API 基础地址
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.github.com";
    
    /// <summary>
    /// OAuth 授权地址
    /// </summary>
    public string OAuthBaseUrl { get; set; } = "https://github.com";
    
    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// 分页大小
    /// </summary>
    public int PageSize { get; set; } = 30;
    
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// 请求频率限制（每秒请求数）
    /// </summary>
    public int RateLimitPerSecond { get; set; } = 10;
}
