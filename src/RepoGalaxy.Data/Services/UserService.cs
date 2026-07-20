using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

public sealed class UserService : IUserService
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly ISecureStorage _secureStorage;
    private readonly SemaphoreSlim _preferencesGate = new(1, 1);
    private UserPreference? _cachedPreferences;
    private DateTimeOffset _preferencesCachedAt;
    public UserService(IDbContextFactory<RepoGalaxyDbContext> factory, ISecureStorage secureStorage) { _factory = factory; _secureStorage = secureStorage; }

    public async Task<bool> IsAuthenticatedAsync() => !string.IsNullOrWhiteSpace(await GetAccessTokenAsync());
    public async Task<User?> GetCurrentUserAsync() { await using var db = await _factory.CreateDbContextAsync(); var entity = await db.Users.AsNoTracking().OrderByDescending(x => x.LastLoginAt).ThenBy(x => x.Id).FirstOrDefaultAsync(); if (entity is null) return null; var model = Map(entity); model.AccessToken = await GetAccessTokenAsync(); return model; }
    public async Task<bool> SaveUserAsync(User user) { await using var db = await _factory.CreateDbContextAsync(); var entity = await db.Users.OrderByDescending(x => x.LastLoginAt).ThenBy(x => x.Id).FirstOrDefaultAsync(); if (entity is null) { entity = new UserEntity(); db.Add(entity); } entity.GitHubId = user.GitHubId; entity.Login = user.Login; entity.AvatarUrl = user.AvatarUrl; entity.Bio = user.Bio; entity.PublicRepos = user.PublicRepos; entity.Followers = user.Followers; entity.LastLoginAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); user.Id = entity.Id; return true; }
    public Task<string?> GetAccessTokenAsync() => _secureStorage.GetAsync("github_access_token");
    public async Task<bool> SaveAccessTokenAsync(string token, DateTimeOffset? expiresAt = null) { if (!await _secureStorage.SetAsync("github_access_token", token)) return false; return !expiresAt.HasValue || await _secureStorage.SetAsync("github_token_expires_at", expiresAt.Value.ToString("O")); }
    public async Task<bool> ClearAuthenticationAsync() { var a = await _secureStorage.RemoveAsync("github_access_token"); var b = await _secureStorage.RemoveAsync("github_token_expires_at"); return a && b; }

    public async Task<UserPreference> GetPreferencesAsync()
    {
        if (_cachedPreferences is not null && DateTimeOffset.UtcNow - _preferencesCachedAt < TimeSpan.FromMinutes(5)) return _cachedPreferences;
        await _preferencesGate.WaitAsync();
        try
        {
            if (_cachedPreferences is not null && DateTimeOffset.UtcNow - _preferencesCachedAt < TimeSpan.FromMinutes(5)) return _cachedPreferences;
            await using var db = await _factory.CreateDbContextAsync(); var entity = await db.UserPreferences.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
            _cachedPreferences = entity is null ? Defaults() : Map(entity); _preferencesCachedAt = DateTimeOffset.UtcNow;
            if (entity is null) await SavePreferencesCoreAsync(_cachedPreferences);
            return _cachedPreferences;
        }
        finally { _preferencesGate.Release(); }
    }
    public async Task<bool> SavePreferencesAsync(UserPreference preferences) { await _preferencesGate.WaitAsync(); try { return await SavePreferencesCoreAsync(preferences); } finally { _preferencesGate.Release(); } }
    private async Task<bool> SavePreferencesCoreAsync(UserPreference p)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync(); var e = await db.UserPreferences.OrderBy(x => x.Id).FirstOrDefaultAsync(); if (e is null) { e = new UserPreferenceEntity(); db.Add(e); }
            e.UserId = p.UserId; e.InterestedTopicsJson = JsonSerializer.Serialize(p.InterestedTopics); e.InterestedLanguagesJson = JsonSerializer.Serialize(p.InterestedLanguages); e.IgnoredTopicsJson = JsonSerializer.Serialize(p.IgnoredTopics); e.MinStarsThreshold = p.MinStarsThreshold; e.MaxStarsThreshold = p.MaxStarsThreshold; e.PreferFreshContent = p.PreferFreshContent; e.IncludeTrending = p.IncludeTrending; e.PreferSmallProjects = p.PreferSmallProjects; e.DarkMode = p.DarkMode; e.UseSystemTheme = p.UseSystemTheme; e.FeedPageSize = p.FeedPageSize; e.SyncIntervalMinutes = p.SyncIntervalMinutes; e.NotificationThreshold = p.NotificationThreshold; e.MaxCacheSizeGB = p.MaxCacheSizeGB; e.AutoCleanCache = p.AutoCleanCache; e.MemoryCacheSizeMB = p.MemoryCacheSizeMB; e.PersistentCacheSizeMB = p.PersistentCacheSizeMB; e.FeedCacheTtlMinutes = p.FeedCacheTtlMinutes; e.DetailCacheTtlMinutes = p.DetailCacheTtlMinutes; e.NewsCacheTtlMinutes = p.NewsCacheTtlMinutes; e.CachePreset = p.CachePreset; e.LastUpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(); p.Id = e.Id; _cachedPreferences = p; _preferencesCachedAt = DateTimeOffset.UtcNow; return true;
        }
        catch { return false; }
    }
    public async Task UpdateInterestedTopicsFromHistoryAsync() { await using var db = await _factory.CreateDbContextAsync(); var ids = (await db.ViewHistories.AsNoTracking().OrderByDescending(x => x.ViewedAt).Take(50).Select(x => x.RepositoryId).ToListAsync()).Distinct().ToList(); var json = await db.Repositories.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => x.TopicsJson).ToListAsync(); var top = json.SelectMany(Read).GroupBy(x => x, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Count()).Take(10).Select(x => x.Key); var p = await GetPreferencesAsync(); p.InterestedTopics = p.InterestedTopics.Concat(top).Distinct(StringComparer.OrdinalIgnoreCase).Take(15).ToList(); await SavePreferencesAsync(p); }
    public async Task AddInterestedLanguageAsync(string language) { if (string.IsNullOrWhiteSpace(language)) return; var p = await GetPreferencesAsync(); if (!p.InterestedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase)) { p.InterestedLanguages.Add(language); await SavePreferencesAsync(p); } }
    public async Task RemoveInterestedLanguageAsync(string language) { var p = await GetPreferencesAsync(); p.InterestedLanguages = p.InterestedLanguages.Where(x => !x.Equals(language, StringComparison.OrdinalIgnoreCase)).ToList(); await SavePreferencesAsync(p); }
    public void ClearPreferencesCache() { _cachedPreferences = null; _preferencesCachedAt = default; }

    private static User Map(UserEntity e) => new() { Id = e.Id, GitHubId = e.GitHubId, Login = e.Login, AvatarUrl = e.AvatarUrl ?? string.Empty, Bio = e.Bio ?? string.Empty, PublicRepos = e.PublicRepos, Followers = e.Followers, LastLoginAt = e.LastLoginAt };
    private static UserPreference Map(UserPreferenceEntity e) => new() { Id = e.Id, UserId = e.UserId, InterestedTopics = Read(e.InterestedTopicsJson), InterestedLanguages = Read(e.InterestedLanguagesJson), IgnoredTopics = Read(e.IgnoredTopicsJson), MinStarsThreshold = e.MinStarsThreshold, MaxStarsThreshold = e.MaxStarsThreshold, PreferFreshContent = e.PreferFreshContent, IncludeTrending = e.IncludeTrending, PreferSmallProjects = e.PreferSmallProjects, DarkMode = e.DarkMode, UseSystemTheme = e.UseSystemTheme ?? true, FeedPageSize = e.FeedPageSize, SyncIntervalMinutes = e.SyncIntervalMinutes, NotificationThreshold = e.NotificationThreshold, MaxCacheSizeGB = e.MaxCacheSizeGB, AutoCleanCache = e.AutoCleanCache, MemoryCacheSizeMB = e.MemoryCacheSizeMB, PersistentCacheSizeMB = e.PersistentCacheSizeMB, FeedCacheTtlMinutes = e.FeedCacheTtlMinutes, DetailCacheTtlMinutes = e.DetailCacheTtlMinutes, NewsCacheTtlMinutes = e.NewsCacheTtlMinutes, CachePreset = e.CachePreset };
    private static UserPreference Defaults() => new();
    private static List<string> Read(string? json) { try { return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? []; } catch { return []; } }
}
