using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RepoGalaxy.Core.Interfaces;

namespace RepoGalaxy.Desktop.Services;

/// <summary>Uses the native credential vault on Windows and macOS.</summary>
public sealed class SecureStorage : ISecureStorage
{
    private readonly string _directory;
    private const string KeychainService = "RepoGalaxy.Credentials";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(KeychainService);

    public SecureStorage()
    {
        _directory = Path.Combine(ApplicationPaths.GetDataDirectory(), "Credentials");
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(_directory);
    }

    public Task<bool> SetAsync(string key, string value)
    {
        try
        {
            if (OperatingSystem.IsMacOS()) return Task.FromResult(SetMacKeychain(key, value));
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
            if (OperatingSystem.IsMacOS()) return Task.FromResult(GetMacKeychain(key));
            var path = GetPath(key);
            if (!File.Exists(path) || !OperatingSystem.IsWindows()) return Task.FromResult<string?>(null);
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(plain));
        }
        catch { return Task.FromResult<string?>(null); }
    }

    public Task<bool> RemoveAsync(string key)
    {
        if (OperatingSystem.IsMacOS()) return Task.FromResult(RemoveMacKeychain(key));
        try { var path = GetPath(key); if (File.Exists(path)) File.Delete(path); return Task.FromResult(true); }
        catch { return Task.FromResult(false); }
    }

    public Task<bool> ContainsKeyAsync(string key)
    {
        if (!OperatingSystem.IsMacOS()) return Task.FromResult(OperatingSystem.IsWindows() && File.Exists(GetPath(key)));

        var (service, account) = GetMacKeychainIdentity(key);
        var status = FindMacKeychainItem(service, account, out var length, out var password, out var item);
        if (status != MacKeychain.Success) return Task.FromResult(false);
        FreeMacKeychainItem(length, password, item);
        return Task.FromResult(true);
    }

    private string GetPath(string key) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".bin");

    private static bool SetMacKeychain(string key, string value)
    {
        var (service, account) = GetMacKeychainIdentity(key);
        var password = Encoding.UTF8.GetBytes(value);
        try
        {
            var status = FindMacKeychainItem(service, account, out var length, out var existingPassword, out var item);
            if (status == MacKeychain.Success)
            {
                try
                {
                    using var passwordHandle = new PinnedBuffer(password);
                    return MacKeychain.SecKeychainItemModifyAttributesAndData(
                        item, IntPtr.Zero, (uint)password.Length, passwordHandle.Pointer) == MacKeychain.Success;
                }
                finally { FreeMacKeychainItem(length, existingPassword, item); }
            }

            if (status != MacKeychain.ItemNotFound) return false;
            using var newPasswordHandle = new PinnedBuffer(password);
            var addStatus = MacKeychain.SecKeychainAddGenericPassword(
                IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
                (uint)password.Length, newPasswordHandle.Pointer, out var newItem);
            return addStatus == MacKeychain.Success && ReleaseMacKeychainItem(newItem);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }

    private static string? GetMacKeychain(string key)
    {
        var (service, account) = GetMacKeychainIdentity(key);
        var status = FindMacKeychainItem(service, account, out var length, out var password, out var item);
        if (status != MacKeychain.Success) return null;

        byte[]? plain = null;
        try
        {
            if (length > int.MaxValue || (length > 0 && password == IntPtr.Zero)) return null;
            plain = new byte[(int)length];
            if (length > 0) Marshal.Copy(password, plain, 0, plain.Length);
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
            FreeMacKeychainItem(length, password, item);
        }
    }

    private static bool RemoveMacKeychain(string key)
    {
        var (service, account) = GetMacKeychainIdentity(key);
        var status = FindMacKeychainItem(service, account, out var length, out var password, out var item);
        if (status == MacKeychain.ItemNotFound) return true;
        if (status != MacKeychain.Success) return false;

        try { return MacKeychain.SecKeychainItemDelete(item) == MacKeychain.Success; }
        finally { FreeMacKeychainItem(length, password, item); }
    }

    private static (byte[] Service, byte[] Account) GetMacKeychainIdentity(string key) =>
        (Encoding.UTF8.GetBytes(KeychainService),
            Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))));

    private static int FindMacKeychainItem(
        byte[] service, byte[] account, out uint passwordLength, out IntPtr password, out IntPtr item) =>
        MacKeychain.SecKeychainFindGenericPassword(
            IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
            out passwordLength, out password, out item);

    private static void FreeMacKeychainItem(uint length, IntPtr password, IntPtr item)
    {
        if (password != IntPtr.Zero) MacKeychain.SecKeychainItemFreeContent(IntPtr.Zero, password);
        ReleaseMacKeychainItem(item);
    }

    private static bool ReleaseMacKeychainItem(IntPtr item)
    {
        if (item == IntPtr.Zero) return true;
        MacKeychain.CFRelease(item);
        return true;
    }

    private sealed class PinnedBuffer : IDisposable
    {
        private readonly GCHandle _handle;
        public IntPtr Pointer => _handle.AddrOfPinnedObject();

        public PinnedBuffer(byte[] bytes) => _handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        public void Dispose() => _handle.Free();
    }

    private static class MacKeychain
    {
        public const int Success = 0;
        public const int ItemNotFound = -25300;
        private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [DllImport(SecurityFramework)]
        public static extern int SecKeychainFindGenericPassword(
            IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account,
            out uint passwordLength, out IntPtr passwordData, out IntPtr item);

        [DllImport(SecurityFramework)]
        public static extern int SecKeychainAddGenericPassword(
            IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account,
            uint passwordLength, IntPtr passwordData, out IntPtr item);

        [DllImport(SecurityFramework)]
        public static extern int SecKeychainItemDelete(IntPtr item);

        [DllImport(SecurityFramework)]
        public static extern int SecKeychainItemModifyAttributesAndData(
            IntPtr item, IntPtr attributes, uint passwordLength, IntPtr passwordData);

        [DllImport(SecurityFramework)]
        public static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);

        [DllImport(CoreFoundationFramework)]
        public static extern void CFRelease(IntPtr item);
    }
}
