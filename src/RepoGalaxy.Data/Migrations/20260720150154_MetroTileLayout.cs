using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoGalaxy.Data.Migrations
{
    /// <inheritdoc />
    public partial class MetroTileLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerAvatarUrl",
                table: "Repositories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TileBoards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    LayoutVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ViewportX = table.Column<double>(type: "REAL", nullable: false),
                    ViewportY = table.Column<double>(type: "REAL", nullable: false),
                    ExtentColumns = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtentRows = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TileBoards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TilePlacements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentKind = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    ContentKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: true),
                    Column = table.Column<int>(type: "INTEGER", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnSpan = table.Column<int>(type: "INTEGER", nullable: false),
                    RowSpan = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", nullable: false),
                    AccentKey = table.Column<string>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    IsPlaceholder = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TilePlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TilePlacements_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TilePlacements_TileBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "TileBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TileBoards_ScopeKey_Source_LayoutVersion",
                table: "TileBoards",
                columns: new[] { "ScopeKey", "Source", "LayoutVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TilePlacements_BoardId_ContentKind_ContentKey",
                table: "TilePlacements",
                columns: new[] { "BoardId", "ContentKind", "ContentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TilePlacements_RepositoryId",
                table: "TilePlacements",
                column: "RepositoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TilePlacements");

            migrationBuilder.DropTable(
                name: "TileBoards");

            migrationBuilder.DropColumn(
                name: "OwnerAvatarUrl",
                table: "Repositories");
        }
    }
}
