using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

/// <summary>
/// 用户服务接口
/// </summary>
public interface IUserService
{
    // 认证
    Task<User?> GetCurrentUserAsync();
    Task<bool> SaveUserAsync(User user);
    
    // Token 管理
    
    // 偏好设置
    Task<UserPreference> GetPreferencesAsync();
    Task<bool> SavePreferencesAsync(UserPreference preferences);
    
    // 兴趣分析
    Task UpdateInterestedTopicsFromHistoryAsync();
    Task AddInterestedLanguageAsync(string language);
    Task RemoveInterestedLanguageAsync(string language);
}
