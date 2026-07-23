using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RepoGalaxy.Data.DbContexts;

#nullable disable

namespace RepoGalaxy.Data.Migrations;

[DbContext(typeof(RepoGalaxyDbContext))]
[Migration("20260723170000_AddGitHubQuotaPersistence")]
public sealed class AddGitHubQuotaPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApiRequestAggregates",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ScopeKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                HourBucket = table.Column<long>(type: "INTEGER", nullable: false),
                Resource = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                Operation = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                IsNetwork = table.Column<bool>(type: "INTEGER", nullable: false),
                StatusClass = table.Column<int>(type: "INTEGER", nullable: false),
                RequestCount = table.Column<long>(type: "INTEGER", nullable: false),
                TotalDurationMilliseconds = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ApiRequestAggregates", x => x.Id));

        migrationBuilder.CreateTable(
            name: "GitHubRateBudgetSnapshots",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ScopeKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Resource = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                Limit = table.Column<int>(type: "INTEGER", nullable: false),
                Used = table.Column<int>(type: "INTEGER", nullable: false),
                Remaining = table.Column<int>(type: "INTEGER", nullable: false),
                ResetAt = table.Column<long>(type: "INTEGER", nullable: false),
                ObservedAt = table.Column<long>(type: "INTEGER", nullable: false),
                RetryAfter = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_GitHubRateBudgetSnapshots", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ApiRequestAggregates_ScopeKey_HourBucket_Resource_Operation_IsNetwork_StatusClass",
            table: "ApiRequestAggregates",
            columns: new[] { "ScopeKey", "HourBucket", "Resource", "Operation", "IsNetwork", "StatusClass" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GitHubRateBudgetSnapshots_ScopeKey_Resource",
            table: "GitHubRateBudgetSnapshots",
            columns: new[] { "ScopeKey", "Resource" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ApiRequestAggregates");
        migrationBuilder.DropTable(name: "GitHubRateBudgetSnapshots");
    }
}
