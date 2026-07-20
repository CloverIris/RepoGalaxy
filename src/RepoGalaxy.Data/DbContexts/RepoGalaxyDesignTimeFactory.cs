using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RepoGalaxy.Data.DbContexts;

public sealed class RepoGalaxyDesignTimeFactory : IDesignTimeDbContextFactory<RepoGalaxyDbContext>
{
    public RepoGalaxyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite("Data Source=repogalaxy-design.db").Options;
        return new RepoGalaxyDbContext(options);
    }
}
