using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using System.Text.Json;

namespace RepoGalaxy.Data.Services;

/// <summary>
/// 用户服务
/// 注意：AccessToken 使用 ISecureStorage 加密存储，不在数据库中明文保存
/// </summary>
public class UserService : IUserService
{
    private readonly RepoGalaxyDbContext _context;
    private readonly ISecureStorage _secureStorage;
    private User? _currentUser;
    private UserPreference? _cachedPreferences;
    private DateTimeOffset _preferencesCacheTime = DateTimeOffset.MinValue;
    private readonly TimeSpan _preferencesCacheDuration = TimeSpan.FromMinutes(5);
    
    public UserService(RepoGalaxyDbContext context, ISecureStorage secureStorage)
    {
        _context = context;
        _secureStorage = secureStorage;
    }
    
    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetAccessTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
    
    public async Task<User?> GetCurrentUserAsync()
    {
        if (_currentUser != null) return _currentUser;
        
        var entity = await _context.Users.FirstOrDefaultAsync();
        if (entity == null) return null;
        
        _currentUser = await MapToModelAsync(entity);
        return _currentUser;
    }
    
    public async Task<bool> SaveUserAsync(User user)
    {
        var existing = await _context.Users.FirstOrDefaultAsync();
        if (existing != null)
        {
            existing.Login = user.Login;
            existing.AvatarUrl = user.AvatarUrl;
            existing.Bio = user.Bio;
            existing.PublicRepos = user.PublicRepos;
            existing.Followers = user.Followers;
            existing.LastLoginAt = DateTimeOffset.Now;
            _context.Users.Update(existing);
        }
        else
        {
            var entity = MapToEntity(user);
            entity.LastLoginAt = DateTimeOffset.Now;
            _context.Users.Add(entity);
        }
        
        await _context.SaveChangesAsync();
        _currentUser = user;
        return true;
    }
    
    public async Task<string?> GetAccessTokenAsync()
    {
        // 使用 SecureStorage 获取 Token，不在数据库中存储
        return await _secureStorage.GetAsync("github_access_token");
    }
    
    public async Task<bool> SaveAccessTokenAsync(string token, DateTimeOffset? expiresAt = null)
    {
        // 使用 SecureStorage 加密存储 Token
        var success = await _secureStorage.SetAsync("github_access_token", token);
        
        if (success && expiresAt.HasValue)
        {
            await _secureStorage.SetAsync("github_token_expires_at", expiresAt.Value.ToString("O"));
        }
        
        return success;
    }
    
    public async Task<bool> ClearAuthenticationAsync()
    {
        // 清除 SecureStorage 中的 Token
        await _secureStorage.RemoveAsync("github_access_token");
        await _secureStorage.RemoveAsync("github_token_expires_at");
        _currentUser = null;
        return true;
    }
    
    public async Task<UserPreference> GetPreferencesAsync()
    {
        // 检查缓存
        if (_cachedPreferences != null && 
            DateTimeOffset.Now - _preferencesCacheTime < _preferencesCacheDuration)
        {
            return _cachedPreferences;
        }
        
        // 从数据库加载
        var entity = await _context.UserPreferences.FirstOrDefaultAsync();
        
        if (entity != null)
        {
            _cachedPreferences = MapToPreferenceModel(entity);
        }
        else
        {
            // 创建默认偏好设置
            _cachedPreferences = CreateDefaultPreferences();
            // 自动保存到数据库
            await SavePreferencesAsync(_cachedPreferences);
        }
        
        _preferencesCacheTime = DateTimeOffset.Now;
        return _cachedPreferences;
    }
    
    public async Task<bool> SavePreferencesAsync(UserPreference preferences)
    {
        try
        {
            var existing = await _context.UserPreferences.FirstOrDefaultAsync();
            
            if (existing != null)
            {
                // 更新现有记录
                existing.InterestedTopicsJson = JsonSerializer.Serialize(preferences.InterestedTopics);
                existing.InterestedLanguagesJson = JsonSerializer.Serialize(preferences.InterestedLanguages);
                existing.MinStarsThreshold = preferences.MinStarsThreshold;
                existing.MaxStarsThreshold = preferences.MaxStarsThreshold;
                existing.IgnoredTopicsJson = JsonSerializer.Serialize(preferences.IgnoredTopics);
                existing.PreferFreshContent = preferences.PreferFreshContent;
                existing.IncludeTrending = preferences.IncludeTrending;
                existing.PreferSmallProjects = preferences.PreferSmallProjects;
                existing.DarkMode = preferences.DarkMode;
                existing.FeedPageSize = preferences.FeedPageSize;
                existing.MaxCacheSizeGB = preferences.MaxCacheSizeGB;
                existing.AutoCleanCache = preferences.AutoCleanCache;
                existing.LastUpdatedAt = DateTimeOffset.Now;
                
                _context.UserPreferences.Update(existing);
            }
            else
            {
                // 创建新记录
                var entity = new UserPreferenceEntity
                {
                    UserId = preferences.UserId,
                    InterestedTopicsJson = JsonSerializer.Serialize(preferences.InterestedTopics),
                    InterestedLanguagesJson = JsonSerializer.Serialize(preferences.InterestedLanguages),
                    MinStarsThreshold = preferences.MinStarsThreshold,
                    MaxStarsThreshold = preferences.MaxStarsThreshold,
                    IgnoredTopicsJson = JsonSerializer.Serialize(preferences.IgnoredTopics),
                    PreferFreshContent = preferences.PreferFreshContent,
                    IncludeTrending = preferences.IncludeTrending,
                    PreferSmallProjects = preferences.PreferSmallProjects,
                    DarkMode = preferences.DarkMode,
                    FeedPageSize = preferences.FeedPageSize,
                    MaxCacheSizeGB = preferences.MaxCacheSizeGB,
                    AutoCleanCache = preferences.AutoCleanCache,
                    LastUpdatedAt = DateTimeOffset.Now
                };
                
                _context.UserPreferences.Add(entity);
                preferences.Id = entity.Id;
            }
            
            await _context.SaveChangesAsync();
            
            // 更新缓存
            _cachedPreferences = preferences;
            _preferencesCacheTime = DateTimeOffset.Now;
            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    /// <summary>
    /// 更新用户兴趣标签（基于浏览历史）
    /// </summary>
    public async Task UpdateInterestedTopicsFromHistoryAsync()
    {
        // 获取最近浏览的仓库
        var recentViews = await _context.ViewHistories
            .OrderByDescending(v => v.ViewedAt)
            .Take(50)
            .Select(v => v.RepositoryId)
            .ToListAsync();
        
        if (!recentViews.Any()) return;
        
        // 统计这些仓库的主题
        var topics = await _context.Repositories
            .Where(r => recentViews.Contains(r.Id) && r.TopicsJson != null)
            .Select(r => r.TopicsJson)
            .ToListAsync();
        
        var topicCounts = new Dictionary<string, int>();
        foreach (var topicJson in topics)
        {
            if (string.IsNullOrEmpty(topicJson)) continue;
            
            try
            {
                var repoTopics = JsonSerializer.Deserialize<List<string>>(topicJson);
                if (repoTopics != null)
                {
                    foreach (var topic in repoTopics)
                    {
                        if (!topicCounts.ContainsKey(topic))
                            topicCounts[topic] = 0;
                        topicCounts[topic]++;
                    }
                }
            }
            catch { /* 忽略解析错误 */ }
        }
        
        // 获取出现次数最多的前 10 个主题
        var topTopics = topicCounts
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => kv.Key)
            .ToList();
        
        if (!topTopics.Any()) return;
        
        // 更新用户偏好
        var preferences = await GetPreferencesAsync();
        
        // 合并现有兴趣和新的兴趣，去重
        var mergedTopics = preferences.InterestedTopics
            .Concat(topTopics)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15) // 最多保留 15 个
            .ToList();
        
        preferences.InterestedTopics = mergedTopics;
        await SavePreferencesAsync(preferences);
    }
    
    /// <summary>
    /// 添加感兴趣的语言
    /// </summary>
    public async Task AddInterestedLanguageAsync(string language)
    {
        if (string.IsNullOrWhiteSpace(language)) return;
        
        var preferences = await GetPreferencesAsync();
        
        if (!preferences.InterestedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            preferences.InterestedLanguages.Add(language);
            await SavePreferencesAsync(preferences);
        }
    }
    
    /// <summary>
    /// 移除感兴趣的语言
    /// </summary>
    public async Task RemoveInterestedLanguageAsync(string language)
    {
        var preferences = await GetPreferencesAsync();
        
        preferences.InterestedLanguages = preferences.InterestedLanguages
            .Where(l => !l.Equals(language, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        await SavePreferencesAsync(preferences);
    }
    
    /// <summary>
    /// 清除偏好设置缓存（强制下次从数据库重新加载）
    /// </summary>
    public void ClearPreferencesCache()
    {
        _cachedPreferences = null;
        _preferencesCacheTime = DateTimeOffset.MinValue;
    }
    
    private async Task<User> MapToModelAsync(UserEntity entity)
    {
        // 从 SecureStorage 获取 Token
        var token = await _secureStorage.GetAsync("github_access_token");
        var expiryStr = await _secureStorage.GetAsync("github_token_expires_at");
        
        DateTimeOffset? expiry = null;
        if (!string.IsNullOrEmpty(expiryStr) && DateTimeOffset.TryParse(expiryStr, out var parsed))
        {
            expiry = parsed;
        }
        
        return new User
        {
            Id = entity.Id,
            GitHubId = entity.GitHubId,
            Login = entity.Login,
            AvatarUrl = entity.AvatarUrl ?? string.Empty,
            Bio = entity.Bio ?? string.Empty,
            PublicRepos = entity.PublicRepos,
            Followers = entity.Followers,
            AccessToken = token,
            TokenExpiresAt = expiry,
            LastLoginAt = entity.LastLoginAt
        };
    }
    
    private static UserEntity MapToEntity(User model)
    {
        // 注意：Token 不存储在 Entity 中，使用 SecureStorage
        return new UserEntity
        {
            Id = model.Id,
            GitHubId = model.GitHubId,
            Login = model.Login,
            AvatarUrl = model.AvatarUrl,
            Bio = model.Bio,
            PublicRepos = model.PublicRepos,
            Followers = model.Followers,
            LastLoginAt = model.LastLoginAt
        };
    }
    
    private static UserPreference MapToPreferenceModel(UserPreferenceEntity entity)
    {
        return new UserPreference
        {
            Id = entity.Id,
            UserId = entity.UserId,
            InterestedTopics = ParseJsonList(entity.InterestedTopicsJson),
            InterestedLanguages = ParseJsonList(entity.InterestedLanguagesJson),
            MinStarsThreshold = entity.MinStarsThreshold,
            MaxStarsThreshold = entity.MaxStarsThreshold,
            IgnoredTopics = ParseJsonList(entity.IgnoredTopicsJson),
            PreferFreshContent = entity.PreferFreshContent,
            IncludeTrending = entity.IncludeTrending,
            PreferSmallProjects = entity.PreferSmallProjects,
            DarkMode = entity.DarkMode,
            FeedPageSize = entity.FeedPageSize,
            MaxCacheSizeGB = entity.MaxCacheSizeGB,
            AutoCleanCache = entity.AutoCleanCache
        };
    }
    
    private static UserPreference CreateDefaultPreferences()
    {
        return new UserPreference
        {
            InterestedTopics = new List<string>(),
            InterestedLanguages = new List<string>(),
            MinStarsThreshold = 0,
            MaxStarsThreshold = 1000000,
            IgnoredTopics = new List<string>(),
            PreferFreshContent = true,
            IncludeTrending = true,
            PreferSmallProjects = false,
            DarkMode = true,
            FeedPageSize = 50,
            MaxCacheSizeGB = 2,
            AutoCleanCache = true
        };
    }
    
    private static List<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }
}
