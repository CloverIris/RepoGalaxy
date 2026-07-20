using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoGalaxy.Data.Migrations
{
    /// <inheritdoc />
    public partial class IdeTechnologyPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdePreferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TechnologyKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IdeKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdePreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdePreferences_ScopeKey_TechnologyKey",
                table: "IdePreferences",
                columns: new[] { "ScopeKey", "TechnologyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdePreferences");
        }
    }
}
