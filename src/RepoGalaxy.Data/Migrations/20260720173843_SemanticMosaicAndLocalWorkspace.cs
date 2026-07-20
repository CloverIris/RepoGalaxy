using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoGalaxy.Data.Migrations
{
    /// <inheritdoc />
    public partial class SemanticMosaicAndLocalWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CloneOperations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    RepositoryFullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ParentDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    StagingDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    DestinationDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloneOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SemanticIndexPlacements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AccentKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ContentKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    Column = table.Column<int>(type: "INTEGER", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnSpan = table.Column<int>(type: "INTEGER", nullable: false),
                    RowSpan = table.Column<int>(type: "INTEGER", nullable: false),
                    LayoutVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemanticIndexPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SemanticIndexPlacements_TileBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "TileBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CloneOperations_UpdatedAt",
                table: "CloneOperations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SemanticIndexPlacements_BoardId_LayoutVersion_ItemKey",
                table: "SemanticIndexPlacements",
                columns: new[] { "BoardId", "LayoutVersion", "ItemKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloneOperations");

            migrationBuilder.DropTable(
                name: "SemanticIndexPlacements");
        }
    }
}
