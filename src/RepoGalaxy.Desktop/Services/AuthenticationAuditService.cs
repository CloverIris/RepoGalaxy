using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Desktop.Services;

public interface IAuthenticationAuditService
{
    void Record(string action, string outcome, string? detail = null, string? accountId = null);
}

/// <summary>Database-backed, token-free authentication audit trail retained for thirty days.</summary>
public sealed class AuthenticationAuditService : IAuthenticationAuditService
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly object _gate = new();

    public AuthenticationAuditService(IDbContextFactory<RepoGalaxyDbContext> factory)
    {
        _factory = factory;
        Prune();
    }

    public void Record(string action, string outcome, string? detail = null, string? accountId = null)
    {
        try
        {
            var (originPath, errorCode) = Sanitize(detail);
            lock (_gate)
            {
                using var db = _factory.CreateDbContext();
                db.AuthenticationAuditEvents.Add(new AuthenticationAuditEventEntity
                {
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    EventType = SafeCode(action),
                    Outcome = SafeCode(outcome),
                    AccountId = string.IsNullOrWhiteSpace(accountId) ? null : SafeCode(accountId),
                    OriginPath = originPath,
                    ErrorCode = errorCode,
                    OccurredAt = DateTimeOffset.UtcNow
                });
                db.SaveChanges();
            }
        }
        catch
        {
            // Authentication must never fail merely because its audit sink is unavailable.
        }
    }

    private void Prune()
    {
        try
        {
            using var db = _factory.CreateDbContext();
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            db.AuthenticationAuditEvents.Where(x => x.OccurredAt < cutoff).ExecuteDelete();
        }
        catch { }
    }

    private static (string? OriginPath, string? ErrorCode) Sanitize(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return (null, null);
        if (Uri.TryCreate(detail, UriKind.Absolute, out var uri))
            return ($"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}", null);
        return (null, SafeCode(detail));
    }

    private static string SafeCode(string value)
    {
        var safe = new string(value.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':').Take(80).ToArray());
        return string.IsNullOrEmpty(safe) ? "redacted" : safe;
    }
}
