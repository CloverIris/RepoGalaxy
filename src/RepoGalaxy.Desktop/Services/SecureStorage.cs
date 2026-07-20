using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RepoGalaxy.Core.Interfaces;

namespace RepoGalaxy.Desktop.Services;

/// <summary>Windows CurrentUser DPAPI store. Token files are never written in plaintext.</summary>
public sealed class SecureStorage : ISecureStorage
{
    private readonly string _directory;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RepoGalaxy.Credentials.v2");
    public SecureStorage()
    {
        _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RepoGalaxy", "Credentials");
        Directory.CreateDirectory(_directory);
    }

    public Task<bool> SetAsync(string key, string value)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return Task.FromResult(false);
            var target = GetPath(key); var temporary = target + ".tmp";
            var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(temporary, cipher);
            File.Move(temporary, target, true);
            return Task.FromResult(true);
        }
        catch { return Task.FromResult(false); }
    }

    public Task<string?> GetAsync(string key)
    {
        try
        {
            var path = GetPath(key);
            if (!File.Exists(path) || !OperatingSystem.IsWindows()) return Task.FromResult<string?>(null);
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(plain));
        }
        catch { return Task.FromResult<string?>(null); }
    }

    public Task<bool> RemoveAsync(string key)
    {
        try { var path = GetPath(key); if (File.Exists(path)) File.Delete(path); return Task.FromResult(true); }
        catch { return Task.FromResult(false); }
    }
    public Task<bool> ContainsKeyAsync(string key) => Task.FromResult(File.Exists(GetPath(key)));
    private string GetPath(string key) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".bin");
}
