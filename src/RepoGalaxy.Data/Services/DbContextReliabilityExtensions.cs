using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace RepoGalaxy.Data.Services;

public static class DbContextReliabilityExtensions
{
    public static async Task<int> SaveChangesWithRetryAsync(this DbContext db, CancellationToken cancellationToken = default)
    {
        for (var retry = 0; ; retry++)
        {
            try { return await db.SaveChangesAsync(cancellationToken); }
            catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6 && retry < 3)
            {
                var delay = TimeSpan.FromMilliseconds(40 * (1 << retry) + Random.Shared.Next(20, 100));
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
