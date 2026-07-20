using RepoGalaxy.Core.Interfaces;
using System.Text.Json;

namespace RepoGalaxy.GitHub.Services;

/// <summary>
/// GitHub Token 管理器
/// 负责 Token 的存储、获取、过期检查和刷新
/// </summary>
public class GitHubTokenManager
{
    private readonly ISecureStorage _secureStorage;
    private const string TokenKey = "github_access_token";
    private const string TokenExpiryKey = "github_token_expires_at";
    private const string RefreshTokenKey = "github_refresh_token";
    private const string SessionMetadataKey = "github_session_metadata";
    
    public GitHubTokenManager(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
    }
    
    /// <summary>
    /// 保存 Access Token 和 Refresh Token
    /// </summary>
    public async Task<bool> SaveTokenAsync(string accessToken, DateTimeOffset? expiresAt = null, string? refreshToken = null)
    {
        try
        {
            var success = await _secureStorage.SetAsync(TokenKey, accessToken);
            if (!success) return false;
            
            if (expiresAt.HasValue)
            {
                await _secureStorage.SetAsync(TokenExpiryKey, expiresAt.Value.ToString("O"));
            }
            
            // 存储 RefreshToken（用于自动刷新）
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _secureStorage.SetAsync(RefreshTokenKey, refreshToken);
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 获取 Access Token
    /// </summary>
    public async Task<string?> GetTokenAsync()
    {
        return await _secureStorage.GetAsync(TokenKey);
    }
    
    /// <summary>
    /// 获取 Token 过期时间
    /// </summary>
    public async Task<DateTimeOffset?> GetTokenExpiryAsync()
    {
        var expiryStr = await _secureStorage.GetAsync(TokenExpiryKey);
        if (string.IsNullOrEmpty(expiryStr))
            return null;
            
        if (DateTimeOffset.TryParse(expiryStr, out var expiry))
            return expiry;
            
        return null;
    }
    
    /// <summary>
    /// 检查 Token 是否已过期
    /// </summary>
    public async Task<bool> IsTokenExpiredAsync(TimeSpan? buffer = null)
    {
        var expiry = await GetTokenExpiryAsync();
        if (!expiry.HasValue)
            return false; // 无过期时间视为长期有效
            
        var bufferTime = buffer ?? TimeSpan.FromMinutes(5);
        return DateTimeOffset.Now.Add(bufferTime) >= expiry.Value;
    }
    
    /// <summary>
    /// 检查是否有存储的 Token
    /// </summary>
    public async Task<bool> HasTokenAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
    
    /// <summary>
    /// 删除存储的 Token
    /// </summary>
    public async Task<bool> ClearTokenAsync()
    {
        try
        {
            // Clearing to an encrypted empty value keeps compatibility with secure
            // stores that do not support deletion; no credential material remains.
            var tokenCleared = await _secureStorage.SetAsync(TokenKey, "");
            var expiryCleared = await _secureStorage.SetAsync(TokenExpiryKey, "");
            var refreshCleared = await _secureStorage.SetAsync(RefreshTokenKey, "");
            await _secureStorage.RemoveAsync(SessionMetadataKey);
            return tokenCleared && expiryCleared && refreshCleared;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 获取 Refresh Token（用于 Token 刷新）
    /// </summary>
    public async Task<string?> GetRefreshTokenAsync()
    {
        return await _secureStorage.GetAsync(RefreshTokenKey);
    }
    
    /// <summary>
    /// 检查是否需要刷新 Token（即将过期）
    /// </summary>
    public async Task<bool> ShouldRefreshTokenAsync(TimeSpan? buffer = null)
    {
        var expiry = await GetTokenExpiryAsync();
        if (!expiry.HasValue) return false;
        
        var bufferTime = buffer ?? TimeSpan.FromMinutes(5);
        return DateTimeOffset.Now.Add(bufferTime) >= expiry.Value;
    }
    
    /// <summary>
    /// 获取 Token 状态信息（用于显示和调试）
    /// </summary>
    public async Task<TokenStatus> GetTokenStatusAsync()
    {
        var token = await GetTokenAsync();
        var expiry = await GetTokenExpiryAsync();
        var refreshToken = await GetRefreshTokenAsync();
        
        if (string.IsNullOrEmpty(token))
        {
            return new TokenStatus { IsValid = false, Message = "未登录" };
        }
        
        if (!expiry.HasValue)
        {
            return new TokenStatus { IsValid = true, Message = "长期有效" };
        }
        
        var timeUntilExpiry = expiry.Value - DateTimeOffset.Now;
        if (timeUntilExpiry <= TimeSpan.Zero)
        {
            return new TokenStatus 
            { 
                IsValid = false, 
                Message = "Token 已过期",
                CanRefresh = !string.IsNullOrEmpty(refreshToken)
            };
        }
        
        if (timeUntilExpiry < TimeSpan.FromMinutes(5))
        {
            return new TokenStatus 
            { 
                IsValid = true, 
                Message = $"即将过期 ({(int)timeUntilExpiry.TotalSeconds}秒)",
                ExpiresIn = timeUntilExpiry,
                CanRefresh = !string.IsNullOrEmpty(refreshToken)
            };
        }
        
        return new TokenStatus 
        { 
            IsValid = true, 
            Message = $"有效期 {(int)timeUntilExpiry.TotalMinutes} 分钟",
            ExpiresIn = timeUntilExpiry,
            CanRefresh = !string.IsNullOrEmpty(refreshToken)
        };
    }

    public Task<bool> SaveSessionMetadataAsync(string authenticationMethod, string accountLogin, string scope) =>
        _secureStorage.SetAsync(SessionMetadataKey, JsonSerializer.Serialize(new TokenSessionMetadata
        {
            AuthenticationMethod = authenticationMethod,
            AccountLogin = accountLogin,
            Scope = scope,
            VerifiedAt = DateTimeOffset.UtcNow
        }));

    public async Task<TokenSessionMetadata?> GetSessionMetadataAsync()
    {
        var json = await _secureStorage.GetAsync(SessionMetadataKey);
        try { return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<TokenSessionMetadata>(json); }
        catch { return null; }
    }
}

public sealed class TokenSessionMetadata
{
    public string AuthenticationMethod { get; init; } = string.Empty;
    public string AccountLogin { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public DateTimeOffset VerifiedAt { get; init; }
}

/// <summary>
/// Token 状态信息
/// </summary>
public class TokenStatus
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan? ExpiresIn { get; set; }
    public bool CanRefresh { get; set; }
}
