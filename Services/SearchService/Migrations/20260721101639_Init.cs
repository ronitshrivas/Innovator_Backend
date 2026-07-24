using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchService.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FollowGraph",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowGraph", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostIndex",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Avatar = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    HashtagsJson = table.Column<string>(type: "text", nullable: false),
                    CategoriesJson = table.Column<string>(type: "text", nullable: false),
                    ReactionsCount = table.Column<int>(type: "integer", nullable: false),
                    CommentsCount = table.Column<int>(type: "integer", nullable: false),
                    ViewsCount = table.Column<int>(type: "integer", nullable: false),
                    IsReel = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostIndex", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SearchType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserIndex",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Avatar = table.Column<string>(type: "text", nullable: true),
                    Bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    InterestsJson = table.Column<string>(type: "text", nullable: false),
                    FollowersCount = table.Column<int>(type: "integer", nullable: false),
                    FollowingCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIndex", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowGraph_FollowerId",
                table: "FollowGraph",
                column: "FollowerId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowGraph_FollowerId_FollowingId",
                table: "FollowGraph",
                columns: new[] { "FollowerId", "FollowingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FollowGraph_FollowingId",
                table: "FollowGraph",
                column: "FollowingId");

            migrationBuilder.CreateIndex(
                name: "IX_PostIndex_AuthorId",
                table: "PostIndex",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PostIndex_Content",
                table: "PostIndex",
                column: "Content");

            migrationBuilder.CreateIndex(
                name: "IX_PostIndex_PostId",
                table: "PostIndex",
                column: "PostId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistory_UserId",
                table: "SearchHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserIndex_AuthUserId",
                table: "UserIndex",
                column: "AuthUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserIndex_FullName",
                table: "UserIndex",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_UserIndex_Username",
                table: "UserIndex",
                column: "Username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FollowGraph");

            migrationBuilder.DropTable(
                name: "PostIndex");

            migrationBuilder.DropTable(
                name: "SearchHistory");

            migrationBuilder.DropTable(
                name: "UserIndex");
        }
    }
}
