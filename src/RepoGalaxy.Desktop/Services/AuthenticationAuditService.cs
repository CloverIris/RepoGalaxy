using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RepoGalaxy.Desktop.Services;

public interface IAuthenticationAuditService { void Record(string action, string outcome, string? detail = null); }

/// <summary>Local, token-free authentication audit trail retained for thirty days.</summary>
public sealed class AuthenticationAuditService : IAuthenticationAuditService
{
    private readonly string _path;
    private readonly object _gate = new();
    public AuthenticationAuditService()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RepoGalaxy", "Audit");
        Directory.CreateDirectory(directory); _path = Path.Combine(directory, "authentication.jsonl");
        Prune();
    }
    public void Record(string action, string outcome, string? detail = null)
    {
        var entry = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, action, outcome, detail = Sanitize(detail) });
        lock (_gate) File.AppendAllText(_path, entry + Environment.NewLine);
    }
    private void Prune()
    {
        if (!File.Exists(_path)) return;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var retained = File.ReadLines(_path).Where(line => TryKeep(line, cutoff));
        File.WriteAllLines(_path, retained);
    }
    private static bool TryKeep(string line, DateTimeOffset cutoff)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.TryGetProperty("timestamp", out var time) && time.GetDateTimeOffset() >= cutoff;
        }
        catch { return false; }
    }
    private static string? Sanitize(string? detail) => string.IsNullOrWhiteSpace(detail) ? null : detail.Split('?', '#')[0].Replace("token", "[redacted]", StringComparison.OrdinalIgnoreCase).Replace("code", "[redacted]", StringComparison.OrdinalIgnoreCase).Replace("state", "[redacted]", StringComparison.OrdinalIgnoreCase);
}
