namespace RepoGalaxy.Core.Interfaces;

/// <summary>
/// 缓存服务接口
/// </summary>
public interface ICacheService
{
    // 基础操作
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
    
    // 批量操作
    Task<IEnumerable<T>> GetManyAsync<T>(IEnumerable<string> keys) where T : class;
    Task SetManyAsync<T>(IDictionary<string, T> items, TimeSpan? expiration = null) where T : class;
    
    // 清理
    Task ClearAsync();
    Task ClearExpiredAsync();
    Task<long> GetCacheSizeAsync();
    
    // 缓存键生成
    string GenerateKey(string prefix, params object[] parts);
}
