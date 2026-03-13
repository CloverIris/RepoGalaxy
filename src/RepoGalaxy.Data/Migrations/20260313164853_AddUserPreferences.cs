using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoGalaxy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalRepositories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                    GitHubUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsTracked = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalRepositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GitHubId = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryLanguage = table.Column<string>(type: "TEXT", nullable: true),
                    TopicsJson = table.Column<string>(type: "TEXT", nullable: true),
                    HtmlUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    Forks = table.Column<int>(type: "INTEGER", nullable: false),
                    Watchers = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenIssues = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastPushedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    OrbitCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscoveryScore = table.Column<double>(type: "REAL", nullable: false),
                    IsBookmarked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsIgnored = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastViewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ViewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CachedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LanguagesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    InterestedTopicsJson = table.Column<string>(type: "TEXT", nullable: true),
                    InterestedLanguagesJson = table.Column<string>(type: "TEXT", nullable: true),
                    MinStarsThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxStarsThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    IgnoredTopicsJson = table.Column<string>(type: "TEXT", nullable: true),
                    PreferFreshContent = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludeTrending = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferSmallProjects = table.Column<bool>(type: "INTEGER", nullable: false),
                    DarkMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    FeedPageSize = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxCacheSizeGB = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoCleanCache = table.Column<bool>(type: "INTEGER", nullable: false),
                    DiscoveryScoreWeight = table.Column<double>(type: "REAL", nullable: false),
                    ActivityScoreWeight = table.Column<double>(type: "REAL", nullable: false),
                    InterestMatchWeight = table.Column<double>(type: "REAL", nullable: false),
                    QualityScoreWeight = table.Column<double>(type: "REAL", nullable: false),
                    PreferenceWeight = table.Column<double>(type: "REAL", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GitHubId = table.Column<string>(type: "TEXT", nullable: false),
                    Login = table.Column<string>(type: "TEXT", nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Bio = table.Column<string>(type: "TEXT", nullable: true),
                    PublicRepos = table.Column<int>(type: "INTEGER", nullable: false),
                    Followers = table.Column<int>(type: "INTEGER", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bookmarks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    BookmarkedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CollectionName = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookmarks_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ViewHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    ViewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferrerTopic = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViewHistories_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_RepositoryId",
                table: "Bookmarks",
                column: "RepositoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalRepositories_LocalPath",
                table: "LocalRepositories",
                column: "LocalPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_CachedAt",
                table: "Repositories",
                column: "CachedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_IsBookmarked",
                table: "Repositories",
                column: "IsBookmarked");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Owner_Name",
                table: "Repositories",
                columns: new[] { "Owner", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Stars",
                table: "Repositories",
                column: "Stars");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_UpdatedAt",
                table: "Repositories",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Login",
                table: "Users",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViewHistories_RepositoryId",
                table: "ViewHistories",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewHistories_ViewedAt",
                table: "ViewHistories",
                column: "ViewedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookmarks");

            migrationBuilder.DropTable(
                name: "LocalRepositories");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ViewHistories");

            migrationBuilder.DropTable(
                name: "Repositories");
        }
    }
}
