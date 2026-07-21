using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RepoGalaxy.Data.DbContexts;

#nullable disable

namespace RepoGalaxy.Data.Migrations;

[DbContext(typeof(RepoGalaxyDbContext))]
[Migration("20260721090000_RankingTuningProfiles")]
public partial class RankingTuningProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "ProfileRevision", table: "RankingBatches", type: "INTEGER", nullable: false, defaultValue: 1);

        migrationBuilder.CreateTable(
            name: "RankingTuningProfiles",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                ScopeKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Preset = table.Column<int>(type: "INTEGER", nullable: false),
                CoarseRuleMatch = table.Column<double>(type: "REAL", nullable: false),
                CoarseFreshness = table.Column<double>(type: "REAL", nullable: false),
                CoarseStarVelocity = table.Column<double>(type: "REAL", nullable: false),
                CoarseQuality = table.Column<double>(type: "REAL", nullable: false),
                CoarsePreference = table.Column<double>(type: "REAL", nullable: false),
                FineCoarse = table.Column<double>(type: "REAL", nullable: false),
                FineContentProfile = table.Column<double>(type: "REAL", nullable: false),
                FineBehavior = table.Column<double>(type: "REAL", nullable: false),
                FineNovelty = table.Column<double>(type: "REAL", nullable: false),
                FineLocalRelevance = table.Column<double>(type: "REAL", nullable: false),
                ExplorationRatio = table.Column<double>(type: "REAL", nullable: false),
                Temperature = table.Column<double>(type: "REAL", nullable: false),
                FreshnessHalfLifeDays = table.Column<double>(type: "REAL", nullable: false),
                SameLanguagePerTen = table.Column<int>(type: "INTEGER", nullable: false),
                SameOwnerPerTen = table.Column<int>(type: "INTEGER", nullable: false),
                CoarseCandidateCount = table.Column<int>(type: "INTEGER", nullable: false),
                FineResultCount = table.Column<int>(type: "INTEGER", nullable: false),
                Revision = table.Column<int>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_RankingTuningProfiles", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_RankingTuningProfiles_ScopeKey", table: "RankingTuningProfiles", column: "ScopeKey", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RankingTuningProfiles");
        migrationBuilder.DropColumn(name: "ProfileRevision", table: "RankingBatches");
    }
}
