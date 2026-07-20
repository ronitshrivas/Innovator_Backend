using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FeedService.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Avatar = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "post"),
                    IsReel = table.Column<bool>(type: "boolean", nullable: false),
                    ViewsCount = table.Column<int>(type: "integer", nullable: false),
                    SharedPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Posts_Posts_SharedPostId",
                        column: x => x.SharedPostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Avatar = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Comments_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comments_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostCategories",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostCategories", x => new { x.PostId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_PostCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostCategories_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    File = table.Column<string>(type: "text", nullable: false),
                    MediaType = table.Column<string>(type: "text", nullable: false),
                    Thumbnail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostMedia_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reactions_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "CreatedAt", "Description", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9890), null, "Technology", new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9890) },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9890), null, "Business", new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9900) },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9900), null, "Science", new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9900) },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9900), null, "Education", new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9900) },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9900), null, "Innovation", new DateTime(2026, 7, 20, 7, 30, 45, 7, DateTimeKind.Utc).AddTicks(9910) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentId",
                table: "Comments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId",
                table: "Comments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostCategories_CategoryId",
                table: "PostCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_PostId",
                table: "PostMedia",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId",
                table: "Posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedAt",
                table: "Posts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_IsReel",
                table: "Posts",
                column: "IsReel");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_SharedPostId",
                table: "Posts",
                column: "SharedPostId");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostId_AuthorId",
                table: "Reactions",
                columns: new[] { "PostId", "AuthorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "PostCategories");

            migrationBuilder.DropTable(
                name: "PostMedia");

            migrationBuilder.DropTable(
                name: "Reactions");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Posts");
        }
    }
}
