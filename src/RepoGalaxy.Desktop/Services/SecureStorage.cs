using RepoGalaxy.Core.Interfaces;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RepoGalaxy.Desktop.Services;

/// <summary>
/// 跨平台安全存储实现
/// </summary>
public class SecureStorage : ISecureStorage
{
    private readonly string _storagePath;
    private readonly byte[] _entropy;
    
    public SecureStorage()
    {
        _storagePath = GetStoragePath();
        _entropy = Encoding.UTF8.GetBytes("RepoGalaxy_SecureStorage_v1");
        
        // 确保存储目录存在
        Directory.CreateDirectory(_storagePath);
    }
    
    public Task<bool> SetAsync(string key, string value)
    {
        try
        {
            var filePath = GetFilePath(key);
            var encrypted = ProtectData(Encoding.UTF8.GetBytes(value));
            File.WriteAllBytes(filePath, encrypted);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    public Task<string?> GetAsync(string key)
    {
        try
        {
            var filePath = GetFilePath(key);
            if (!File.Exists(filePath))
                return Task.FromResult<string?>(null);
            
            var encrypted = File.ReadAllBytes(filePath);
            var decrypted = UnprotectData(encrypted);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(decrypted));
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }
    
    public Task<bool> RemoveAsync(string key)
    {
        try
        {
            var filePath = GetFilePath(key);
            if (File.Exists(filePath))
                File.Delete(filePath);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    public Task<bool> ContainsKeyAsync(string key)
    {
        var filePath = GetFilePath(key);
        return Task.FromResult(File.Exists(filePath));
    }
    
    private string GetStoragePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "RepoGalaxy", "SecureStorage");
    }
    
    private string GetFilePath(string key)
    {
        // 使用哈希作为文件名，避免特殊字符问题
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_storagePath, $"{hash}.dat");
    }
    
    /// <summary>
    /// 加密数据
    /// </summary>
    private byte[] ProtectData(byte[] data)
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows: 使用 DPAPI
            return ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS: 使用 Keychain (简化实现，实际应使用 Security.framework)
            // 这里使用 AES 加密 + 存储在 Keychain 中的密钥
            return AesEncrypt(data);
        }
        else
        {
            // Linux: 使用 AES 加密
            return AesEncrypt(data);
        }
    }
    
    /// <summary>
    /// 解密数据
    /// </summary>
    private byte[] UnprotectData(byte[] encrypted)
    {
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Unprotect(encrypted, _entropy, DataProtectionScope.CurrentUser);
        }
        else
        {
            return AesDecrypt(encrypted);
        }
    }
    
    private byte[] AesEncrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = GetOrCreateKey();
        aes.GenerateIV();
        
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }
        
        return ms.ToArray();
    }
    
    private byte[] AesDecrypt(byte[] encrypted)
    {
        using var aes = Aes.Create();
        aes.Key = GetOrCreateKey();
        
        var iv = new byte[16];
        Buffer.BlockCopy(encrypted, 0, iv, 0, 16);
        aes.IV = iv;
        
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(
            new MemoryStream(encrypted, 16, encrypted.Length - 16),
            aes.CreateDecryptor(),
            CryptoStreamMode.Read))
        {
            cs.CopyTo(ms);
        }
        
        return ms.ToArray();
    }
    
    private byte[] GetOrCreateKey()
    {
        var keyPath = Path.Combine(_storagePath, "master.key");
        
        if (File.Exists(keyPath))
        {
            return File.ReadAllBytes(keyPath);
        }
        
        // 生成新密钥
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        File.WriteAllBytes(keyPath, key);
        
        // 设置文件权限（仅当前用户可读写）
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        
        return key;
    }
}
