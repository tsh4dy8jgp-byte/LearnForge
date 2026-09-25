using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LearnForge.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class LearningRecordFoundationPostgres : Migration
    {
        // Completion becomes pack-scoped: the first completion of each lesson across all releases wins.
        private const string CopyCompletions = """
            INSERT INTO "LessonProgress" ("UserId", "PackId", "LessonId", "CompletedAt", "ContentHash")
            SELECT c."UserId", p."PackId", c."LessonId", MIN(c."At"), NULL
            FROM "Completions" c JOIN "Packs" p ON p."Id" = c."ReleaseId"
            GROUP BY c."UserId", p."PackId", c."LessonId";
            """;

        // Rollback maps each lesson back onto the pack's latest release.
        private const string RestoreCompletions = """
            INSERT INTO "Completions" ("UserId", "ReleaseId", "LessonId", "At")
            SELECT lp."UserId", (SELECT p."Id" FROM "Packs" p WHERE p."PackId" = lp."PackId" ORDER BY p."PublishedAt" DESC LIMIT 1), lp."LessonId", lp."CompletedAt"
            FROM "LessonProgress" lp
            WHERE EXISTS (SELECT 1 FROM "Packs" p WHERE p."PackId" = lp."PackId");
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FocusObjectiveId",
                table: "Attempts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PackId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => new { x.UserId, x.PackId });
                    table.ForeignKey(
                        name: "FK_Enrollments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PackId = table.Column<string>(type: "text", nullable: false),
                    ReleaseId = table.Column<string>(type: "text", nullable: false),
                    AttemptId = table.Column<string>(type: "text", nullable: false),
                    QuestionId = table.Column<string>(type: "text", nullable: false),
                    FamilyId = table.Column<string>(type: "text", nullable: false),
                    ObjectiveIds = table.Column<string[]>(type: "text[]", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Answered = table.Column<bool>(type: "boolean", nullable: false),
                    FullyCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Earned = table.Column<decimal>(type: "numeric", nullable: false),
                    Possible = table.Column<decimal>(type: "numeric", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evidence_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Evidence_Attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "Attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonProgress",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PackId = table.Column<string>(type: "text", nullable: false),
                    LessonId = table.Column<string>(type: "text", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonProgress", x => new { x.UserId, x.PackId, x.LessonId });
                    table.ForeignKey(
                        name: "FK_LessonProgress_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_AttemptId_QuestionId",
                table: "Evidence",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_UserId_PackId_At",
                table: "Evidence",
                columns: new[] { "UserId", "PackId", "At" });

            migrationBuilder.Sql(CopyCompletions);

            migrationBuilder.DropTable(
                name: "Completions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "Evidence");

            migrationBuilder.DropColumn(
                name: "FocusObjectiveId",
                table: "Attempts");

            migrationBuilder.CreateTable(
                name: "Completions",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ReleaseId = table.Column<string>(type: "text", nullable: false),
                    LessonId = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Completions", x => new { x.UserId, x.ReleaseId, x.LessonId });
                    table.ForeignKey(
                        name: "FK_Completions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(RestoreCompletions);

            migrationBuilder.DropTable(
                name: "LessonProgress");
        }
    }
}
