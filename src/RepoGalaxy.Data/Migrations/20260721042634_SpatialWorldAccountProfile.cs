using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoGalaxy.Data.Migrations
{
    /// <inheritdoc />
    public partial class SpatialWorldAccountProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Blog",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Following",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileUrl",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitterUsername",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SemanticViewportHeight",
                table: "TileBoards",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "SemanticViewportUserPositioned",
                table: "TileBoards",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "SemanticViewportWidth",
                table: "TileBoards",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "WorldSeed",
                table: "TileBoards",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Blog",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Company",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Following",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwitterUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SemanticViewportHeight",
                table: "TileBoards");

            migrationBuilder.DropColumn(
                name: "SemanticViewportUserPositioned",
                table: "TileBoards");

            migrationBuilder.DropColumn(
                name: "SemanticViewportWidth",
                table: "TileBoards");

            migrationBuilder.DropColumn(
                name: "WorldSeed",
                table: "TileBoards");
        }
    }
}
