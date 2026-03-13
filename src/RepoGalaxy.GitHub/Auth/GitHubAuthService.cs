using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RepoGalaxy.GitHub.Configuration;

namespace RepoGalaxy.GitHub.Auth;

/// <summary>
/// GitHub OAuth Device Flow 服务
/// 实现标准 OAuth 2.0 Device Authorization Grant
/// 
/// 参考: https://datatracker.ietf.org/doc/html/rfc8628
///       https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow
/// </summary>
public class GitHubAuthService
{
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubAuthService>? _logger;
    
    public GitHubAuthService(
        GitHubOptions options, 
        HttpClient? httpClient = null,
        ILogger<GitHubAuthService>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
    
    /// <summary>
    /// 开始 Device Flow 认证
    /// </summary>
    public async Task<DeviceFlowResponse> StartDeviceFlowAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Starting GitHub Device Flow...");
        
        // 检查 Client ID
        if (string.IsNullOrEmpty(_options.ClientId) ||
            _options.ClientId == "YOUR_CLIENT_ID" ||
            _options.ClientId == "UNCONFIGURED")
        {
            throw new InvalidOperationException(
                "GitHub OAuth Client ID 未配置。\n\n" +
                "开发者需要在 GitHub 创建 OAuth App 并启用 Device Flow。\n" +
                "或者使用 Personal Access Token 登录。");
        }
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.OAuthBaseUrl}/login/device/code")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["scope"] = _options.Scope
            })
        };
        
        request.Headers.Add("Accept", "application/json");
        
        var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("Device flow start failed: {StatusCode}, {Content}", 
                response.StatusCode, content);
            throw new InvalidOperationException(
                $"启动 Device Flow 失败: {response.StatusCode}\n\n" +
                "可能原因：\n" +
                "1. GitHub OAuth App 未启用 Device Flow\n" +
                "2. Client ID 无效\n\n" +
                "请检查 OAuth App 设置。");
        }
        
        var result = ParseJsonOrQueryString(content);
        
        // 检查错误
        if (result.TryGetValue("error", out var error))
        {
            throw new InvalidOperationException(
                $"Device Flow 启动失败: {error}\n\n" +
                $"可能原因：Device Flow 未在 OAuth App 中启用。");
        }
        
        return new DeviceFlowResponse
        {
            DeviceCode = result["device_code"],
            UserCode = result["user_code"],
            VerificationUri = result.GetValueOrDefault("verification_uri", "https://github.com/login/device"),
            VerificationUriComplete = result.GetValueOrDefault("verification_uri_complete"),
            ExpiresIn = int.Parse(result.GetValueOrDefault("expires_in", "900")),
            Interval = int.Parse(result.GetValueOrDefault("interval", "5"))
        };
    }
    
    /// <summary>
    /// 轮询检查 Device Flow 授权结果
    /// 按照 RFC 8628 规范实现，正确处理各种错误码
    /// </summary>
    public async Task<DeviceFlowPollResult> PollForTokenAsync(
        string deviceCode, 
        int intervalSeconds = 5,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.OAuthBaseUrl}/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            })
        };
        
        request.Headers.Add("Accept", "application/json");
        
        _logger?.LogDebug("Polling for token (interval: {Interval}s)...", intervalSeconds);
        
        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            
            _logger?.LogDebug("Poll response: {Content}", content);
            
            var result = ParseJsonOrQueryString(content);
            
            // 检查错误
            if (result.TryGetValue("error", out var error))
            {
                return HandleDeviceFlowError(error, result, intervalSeconds);
            }
            
            // 成功获取 token
            if (result.TryGetValue("access_token", out var accessToken))
            {
                _logger?.LogInformation("Device Flow completed successfully");
                
                return new DeviceFlowPollResult
                {
                    Status = DeviceFlowStatus.Success,
                    Token = new TokenResponse
                    {
                        AccessToken = accessToken,
                        TokenType = result.GetValueOrDefault("token_type", "bearer"),
                        Scope = result.GetValueOrDefault("scope", "")
                    }
                };
            }
            
            // 未知响应
            return new DeviceFlowPollResult
            {
                Status = DeviceFlowStatus.Error,
                ErrorMessage = "Invalid response from server"
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error polling for token");
            return new DeviceFlowPollResult
            {
                Status = DeviceFlowStatus.Error,
                ErrorMessage = $"Network error: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// 处理 Device Flow 错误码（按照 RFC 8628 规范）
    /// </summary>
    private DeviceFlowPollResult HandleDeviceFlowError(
        string error, 
        Dictionary<string, string> result, 
        int currentInterval)
    {
        switch (error)
        {
            case "authorization_pending":
                // 正常情况，用户还未输入代码，继续轮询
                _logger?.LogDebug("Authorization pending, will retry...");
                return new DeviceFlowPollResult
                {
                    Status = DeviceFlowStatus.Pending,
                    NextIntervalSeconds = currentInterval
                };
                
            case "slow_down":
                // 请求太频繁，需要增加轮询间隔
                var newInterval = currentInterval + 5;
                _logger?.LogWarning("Rate limited, increasing interval to {Interval}s", newInterval);
                return new DeviceFlowPollResult
                {
                    Status = DeviceFlowStatus.SlowDown,
                    NextIntervalSeconds = newInterval
                };
                
            case "expired_token":
                _logger?.LogWarning("Device code expired");
                return new DeviceFlowPollResult
                {
                    Status = DeviceFlowStatus.Expired,
                    ErrorMessage = "授权已过期，请重新尝试登录"
                };
                
            case "access_denied":
                _logger?.LogWarning("User denied access");
                return new DeviceFlowPollResult
                {
                    Status = DeviceFlowStatus.AccessDenied,
                    ErrorMessage = "用户取消了授权"
                };
                
            case "incorrect_client_credentials":
                _logger?.LogError("Invalid client credentials");
                return new DeviceFlowPollResult
                {
                    Status = DeviceFlowStatus.Error,
                    ErrorMessage = "Client ID 无效，请联系开发者"
                };
                
            case "unsupported_grant_type":
                _logger?.LogError("Unsupported grant type");
                return new DeviceFlowPollResult
                {
                    Status = DeviceFlowStatus.Error,
                    ErrorMessage = "Device Flow 不被支持，请检查 OAuth App 设置"
                };
                
            default:
                var description = result.GetValueOrDefault("error_description", "Unknown error");
                _logger?.LogError("Device flow error: {Error} - {Description}", error, description);
                return new DeviceFlowPollResult
                {
                    Status = DeviceFlowStatus.Error,
                    ErrorMessage = $"授权失败: {error} - {description}"
                };
        }
    }
    
    /// <summary>
    /// 打开浏览器到设备验证页面
    /// </summary>
    public void OpenVerificationPage(string? uri = null)
    {
        var targetUri = uri ?? "https://github.com/login/device";
        
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start \"\" \"{targetUri}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{targetUri}\"",
                    UseShellExecute = false
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = targetUri,
                    UseShellExecute = false
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open browser");
        }
    }
    
    /// <summary>
    /// 验证 PAT 是否有效
    /// </summary>
    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("User-Agent", "RepoGalaxy");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            
            var response = await client.GetAsync("https://api.github.com/user");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 解析响应（支持 JSON 或 query string 格式）
    /// </summary>
    private static Dictionary<string, string> ParseJsonOrQueryString(string content)
    {
        content = content.Trim();
        
        // 尝试解析为 JSON
        if (content.StartsWith("{") && content.EndsWith("}"))
        {
            try
            {
                return ParseJsonObject(content);
            }
            catch
            {
                // 回退到 query string 解析
            }
        }
        
        // 解析为 query string
        return ParseQueryString(content);
    }
    
    /// <summary>
    /// 简单解析 JSON 对象（不依赖外部库）
    /// </summary>
    private static Dictionary<string, string> ParseJsonObject(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // 移除最外层的大括号
        json = json.Trim()[1..^1];
        
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
        
        return result;
    }
    
    /// <summary>
    /// 分割 JSON 键值对（处理嵌套）
    /// </summary>
    private static List<string> SplitJsonPairs(string json)
    {
        var pairs = new List<string>();
        var depth = 0;
        var start = 0;
        var inString = false;
        
        for (int i = 0; i < json.Length; i++)
        {
            var c = json[i];
            
            if (c == '"' && (i == 0 || json[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (!inString)
            {
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    pairs.Add(json[start..i]);
                    start = i + 1;
                }
            }
        }
        
        if (start < json.Length)
        {
            pairs.Add(json[start..]);
        }
        
        return pairs;
    }
    
    /// <summary>
    /// 解析 query string
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

/// <summary>
/// Device Flow 启动响应
/// </summary>
public class DeviceFlowResponse
{
    public string DeviceCode { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string VerificationUri { get; set; } = string.Empty;
    public string? VerificationUriComplete { get; set; }
    public int ExpiresIn { get; set; }
    public int Interval { get; set; }
}

/// <summary>
/// Device Flow 轮询结果
/// </summary>
public class DeviceFlowPollResult
{
    public DeviceFlowStatus Status { get; set; }
    public TokenResponse? Token { get; set; }
    public string? ErrorMessage { get; set; }
    public int NextIntervalSeconds { get; set; }
}

/// <summary>
/// Device Flow 状态
/// </summary>
public enum DeviceFlowStatus
{
    /// <summary>
    /// 等待用户授权
    /// </summary>
    Pending,
    
    /// <summary>
    /// 成功获取 token
    /// </summary>
    Success,
    
    /// <summary>
    /// 请求太频繁，需要降低速度
    /// </summary>
    SlowDown,
    
    /// <summary>
    /// 授权码已过期
    /// </summary>
    Expired,
    
    /// <summary>
    /// 用户拒绝授权
    /// </summary>
    AccessDenied,
    
    /// <summary>
    /// 发生错误
    /// </summary>
    Error
}

/// <summary>
/// OAuth Token 响应
/// </summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}
