using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RepoGalaxy.GitHub.Configuration;
using RepoGalaxy.GitHub.Services;

namespace RepoGalaxy.GitHub.Auth;

/// <summary>Optional OAuth code flow. It is enabled only with a locally supplied secret.</summary>
public sealed class OAuthCodeFlowService
{
    private readonly GitHubOptions _options;
    private readonly HttpClient _httpClient;
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.ClientSecret);
    public OAuthCodeFlowService(GitHubOptions options)
    {
        _options = options;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds) };
    }

    public async Task<TokenResponse?> ExecuteOAuthFlowAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("未配置本机 OAuth 回环密钥，请使用设备码登录。");
        using var callback = new LocalCallbackServer();
        var state = CreateState();
        var authorizationUrl = $"{_options.OAuthBaseUrl}/login/oauth/authorize?client_id={Uri.EscapeDataString(_options.ClientId)}&redirect_uri={Uri.EscapeDataString(callback.CallbackUrl)}&scope={Uri.EscapeDataString(_options.Scope)}&state={Uri.EscapeDataString(state)}";
        var callbackTask = callback.WaitForCallbackAsync(timeout ?? TimeSpan.FromMinutes(5), state, cancellationToken);
        OpenBrowser(authorizationUrl);
        var result = await callbackTask;
        if (result == null) return null;
        if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException("GitHub 授权已取消或失败。");
        if (string.IsNullOrEmpty(result.Code) || result.State is null || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(state), System.Text.Encoding.UTF8.GetBytes(result.State)))
            throw new InvalidOperationException("本地回环验证失败，请重新登录。");
        return await ExchangeAsync(result.Code, callback.CallbackUrl, cancellationToken);
    }

    private async Task<TokenResponse> ExchangeAsync(string code, string callbackUrl, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.OAuthBaseUrl}/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = _options.ClientId, ["client_secret"] = _options.ClientSecret!, ["code"] = code, ["redirect_uri"] = callbackUrl })
        };
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!json.RootElement.TryGetProperty("access_token", out var accessToken)) throw new InvalidOperationException("GitHub 未返回访问令牌。");
        return new TokenResponse { AccessToken = accessToken.GetString()!, TokenType = json.RootElement.TryGetProperty("token_type", out var type) ? type.GetString() ?? "bearer" : "bearer", Scope = json.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() ?? string.Empty : string.Empty };
    }

    private static string CreateState() { Span<byte> bytes = stackalloc byte[32]; RandomNumberGenerator.Fill(bytes); return Convert.ToHexString(bytes); }
    private static void OpenBrowser(string uri) => Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
}
