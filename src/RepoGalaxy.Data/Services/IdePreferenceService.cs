using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

public sealed class IdePreferenceService(IDbContextFactory<RepoGalaxyDbContext> factory) : IIdePreferenceService
{
    public async Task<string?> GetPreferredIdeAsync(string scopeKey, string technologyKey, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.IdePreferences.AsNoTracking().Where(x => x.ScopeKey == Normalize(scopeKey) && x.TechnologyKey == Normalize(technologyKey)).Select(x => x.IdeKey).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetPreferredIdeAsync(string scopeKey, string technologyKey, string ideKey, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var scope = Normalize(scopeKey); var technology = Normalize(technologyKey);
        var entity = await db.IdePreferences.FirstOrDefaultAsync(x => x.ScopeKey == scope && x.TechnologyKey == technology, cancellationToken);
        if (entity is null) db.IdePreferences.Add(new IdePreferenceEntity { ScopeKey = scope, TechnologyKey = technology, IdeKey = ideKey, UpdatedAt = DateTimeOffset.UtcNow });
        else { entity.IdeKey = ideKey; entity.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
    }

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? "guest" : value.Trim().ToLowerInvariant();
}
