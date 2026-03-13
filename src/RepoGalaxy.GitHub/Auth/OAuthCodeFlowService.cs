using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using RepoGalaxy.GitHub.Configuration;
using RepoGalaxy.GitHub.Services;

namespace RepoGalaxy.GitHub.Auth;

/// <summary>
/// GitHub OAuth Authorization Code Flow 服务
/// 实现标准 OAuth 2.0 授权码流程
/// 
/// 注意：GitHub OAuth App 要求 client_secret，即使是 PKCE 也不行
/// 参考: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps
/// </summary>
public class OAuthCodeFlowService
{
    private readonly GitHubOptions _options;
    private readonly ILogger<OAuthCodeFlowService>? _logger;
    private readonly HttpClient _httpClient;
    
    public OAuthCodeFlowService(GitHubOptions options, ILogger<OAuthCodeFlowService>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }
    
    /// <summary>
    /// 生成授权 URL
    /// </summary>
    public string GenerateAuthorizationUrl(string callbackUrl, out string state)
    {
        state = GenerateState();
        
        var scopes = string.Join("+", _options.Scope.Split(' '));
        
        var url = $"{_options.OAuthBaseUrl}/login/oauth/authorize" +
                  $"?client_id={_options.ClientId}" +
                  $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
                  $"&scope={scopes}" +
                  $"&state={state}";
        
        _logger?.LogInformation("生成授权 URL: {Url}", url);
        return url;
    }
    
    /// <summary>
    /// 使用授权码交换 Access Token
    /// </summary>
    public async Task<TokenResponse> ExchangeCodeAsync(
        string code, 
        string callbackUrl, 
        string expectedState, 
        string receivedState)
    {
        // 验证 state 防止 CSRF
        if (!string.Equals(expectedState, receivedState, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid state parameter. Possible CSRF attack.");
        }
        
        _logger?.LogInformation("使用授权码交换 Token...");
        
        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret 
                ?? throw new InvalidOperationException("Client Secret 未配置。"),
            ["code"] = code,
            ["redirect_uri"] = callbackUrl
        };
        
        var requestContent = new FormUrlEncodedContent(requestBody);
        
        var response = await _httpClient.PostAsync(
            $"{_options.OAuthBaseUrl}/login/oauth/access_token", 
            requestContent);
        
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("Token exchange failed: {StatusCode}, {Content}", 
                response.StatusCode, content);
            throw new InvalidOperationException($"Token exchange failed: {response.StatusCode}");
        }
        
        var result = ParseJsonResponse(content);
        
        if (result.TryGetValue("error", out var error))
        {
            var errorDescription = result.GetValueOrDefault("error_description", "Unknown error");
            throw new InvalidOperationException($"Token exchange failed: {error} - {errorDescription}");
        }
        
        return new TokenResponse
        {
            AccessToken = result["access_token"],
            TokenType = result.GetValueOrDefault("token_type", "bearer"),
            Scope = result.GetValueOrDefault("scope", "")
        };
    }
    
    /// <summary>
    /// 执行完整的 OAuth 流程
    /// </summary>
    public async Task<TokenResponse?> ExecuteOAuthFlowAsync(
        TimeSpan? timeout = null,
        int callbackPort = 5000,
        CancellationToken ct = default)
    {
        // 检查配置
        if (string.IsNullOrEmpty(_options.ClientId) || 
            _options.ClientId == "YOUR_CLIENT_ID" ||
            _options.ClientId == "UNCONFIGURED")
        {
            throw new InvalidOperationException(
                "GitHub OAuth Client ID 未配置。\n\n" +
                "请设置 GITHUB_CLIENT_ID 和 GITHUB_CLIENT_SECRET 环境变量，\n" +
                "或使用设备码登录作为替代方案。");
        }
        
        if (string.IsNullOrEmpty(_options.ClientSecret))
        {
            throw new InvalidOperationException(
                "GitHub OAuth Client Secret 未配置。\n\n" +
                "GitHub OAuth App 需要 Client Secret。\n" +
                "请设置 GITHUB_CLIENT_SECRET 环境变量，\n" +
                "或使用设备码登录作为替代方案。");
        }
        
        using var callbackServer = new LocalCallbackServer(callbackPort, logger: null);
        
        // 1. 生成授权 URL
        var authUrl = GenerateAuthorizationUrl(callbackServer.CallbackUrl, out var state);
        
        // 2. 拉起浏览器
        OpenBrowser(authUrl);
        
        // 3. 等待回调
        var code = await callbackServer.WaitForCallbackAsync(timeout ?? TimeSpan.FromMinutes(5), ct);
        
        if (string.IsNullOrEmpty(code))
        {
            _logger?.LogWarning("未收到授权码");
            return null;
        }
        
        // 4. 交换 Token
        var token = await ExchangeCodeAsync(code, callbackServer.CallbackUrl, state, state);
        return token;
    }
    
    /// <summary>
    /// 生成随机的 state 参数
    /// </summary>
    private static string GenerateState()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
    
    /// <summary>
    /// 打开浏览器（跨平台支持）
    /// </summary>
    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start \"\" \"{url}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{url}\"",
                    UseShellExecute = false
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                TryOpenBrowserLinux(url);
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法打开浏览器: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Linux 下尝试多种浏览器
    /// </summary>
    private static void TryOpenBrowserLinux(string url)
    {
        string[] browsers = { "xdg-open", "chromium", "chromium-browser", "google-chrome", "firefox" };
        
        foreach (var browser in browsers)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = browser,
                    Arguments = url,
                    UseShellExecute = false
                });
                return;
            }
            catch
            {
                // 继续尝试下一个
            }
        }
        
        throw new InvalidOperationException("无法找到可用的浏览器");
    }
    
    /// <summary>
    /// 解析 JSON 响应
    /// </summary>
    private static Dictionary<string, string> ParseJsonResponse(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            json = json.Trim();
            if (json.StartsWith("{") && json.EndsWith("}"))
            {
                json = json[1..^1];
                
                var pairs = SplitJsonPairs(json);
                foreach (var pair in pairs)
                {
                    var colonIndex = pair.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var key = pair[..colonIndex].Trim().Trim('"');
                        var value = pair[(colonIndex + 1)..].Trim().Trim('"');
                        result[key] = value;
                    }
                }
            }
        }
        catch
        {
            // 回退到 query string 解析
            return ParseQueryString(json);
        }
        
        return result;
    }
    
    /// <summary>
    /// 分割 JSON 键值对
    /// </summary>
    private static List<string> SplitJsonPairs(string json)
    {
        var pairs = new List<string>();
        var depth = 0;
        var start = 0;
        
        for (int i = 0; i < json.Length; i++)
        {
            var c = json[i];
            
            if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
            else if (c == ',' && depth == 0)
            {
                pairs.Add(json[start..i]);
                start = i + 1;
            }
        }
        
        if (start < json.Length)
        {
            pairs.Add(json[start..]);
        }
        
        return pairs;
    }
    
    /// <summary>
    /// 解析 query string（向后兼容）
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0]] = Uri.UnescapeDataString(parts[1]);
            }
        }
        
        return result;
    }
}
