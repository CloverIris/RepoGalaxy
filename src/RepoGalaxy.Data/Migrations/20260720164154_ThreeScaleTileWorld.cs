using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoGalaxy.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThreeScaleTileWorld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // v1 was intentionally one-dimensional. Its coordinates and viewports are not
            // meaningful in the three-scale world; only these two reconstructible tables
            // are cleared. Feed, subscriptions, saved repositories and caches stay intact.
            migrationBuilder.Sql("DELETE FROM TilePlacements;");
            migrationBuilder.Sql("DELETE FROM TileBoards;");

            migrationBuilder.RenameColumn(
                name: "ViewportY",
                table: "TileBoards",
                newName: "Zoom");

            migrationBuilder.RenameColumn(
                name: "ViewportX",
                table: "TileBoards",
                newName: "CameraY");

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "TilePlacements",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActiveIndexKey",
                table: "TileBoards",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ActiveIndexKind",
                table: "TileBoards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CameraX",
                table: "TileBoards",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "TilePlacements");

            migrationBuilder.DropColumn(
                name: "ActiveIndexKey",
                table: "TileBoards");

            migrationBuilder.DropColumn(
                name: "ActiveIndexKind",
                table: "TileBoards");

            migrationBuilder.DropColumn(
                name: "CameraX",
                table: "TileBoards");

            migrationBuilder.RenameColumn(
                name: "Zoom",
                table: "TileBoards",
                newName: "ViewportY");

            migrationBuilder.RenameColumn(
                name: "CameraY",
                table: "TileBoards",
                newName: "ViewportX");
        }
    }
}
