using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoGalaxy.Data.Migrations
{
    /// <inheritdoc />
    public partial class SemanticViewportAndIndexV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "SemanticViewportX",
                table: "TileBoards",
                type: "REAL",
                nullable: false,
                defaultValue: 24.0);

            migrationBuilder.AddColumn<double>(
                name: "SemanticViewportY",
                table: "TileBoards",
                type: "REAL",
                nullable: false,
                defaultValue: 24.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SemanticViewportX",
                table: "TileBoards");

            migrationBuilder.DropColumn(
                name: "SemanticViewportY",
                table: "TileBoards");
        }
    }
}
