using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnForge.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class LearningRecordFoundation : Migration
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PackId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PackId = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseId = table.Column<string>(type: "TEXT", nullable: false),
                    AttemptId = table.Column<string>(type: "TEXT", nullable: false),
                    QuestionId = table.Column<string>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<string>(type: "TEXT", nullable: false),
                    ObjectiveIds = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Answered = table.Column<bool>(type: "INTEGER", nullable: false),
                    FullyCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    Earned = table.Column<decimal>(type: "TEXT", nullable: false),
                    Possible = table.Column<decimal>(type: "TEXT", nullable: false),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PackId = table.Column<string>(type: "TEXT", nullable: false),
                    LessonId = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", nullable: true)
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
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseId = table.Column<string>(type: "TEXT", nullable: false),
                    LessonId = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false)
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
