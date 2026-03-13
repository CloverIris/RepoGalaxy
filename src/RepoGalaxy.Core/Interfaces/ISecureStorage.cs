namespace RepoGalaxy.Core.Interfaces;

/// <summary>
/// 安全存储接口 - 跨平台 Token 存储
/// macOS: Keychain
/// Windows: DPAPI / Credential Manager
/// Linux: Secret Service API
/// </summary>
public interface ISecureStorage
{
    /// <summary>
    /// 安全保存值
    /// </summary>
    Task<bool> SetAsync(string key, string value);
    
    /// <summary>
    /// 获取安全存储的值
    /// </summary>
    Task<string?> GetAsync(string key);
    
    /// <summary>
    /// 删除存储的值
    /// </summary>
    Task<bool> RemoveAsync(string key);
    
    /// <summary>
    /// 检查是否存在
    /// </summary>
    Task<bool> ContainsKeyAsync(string key);
}
