using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnForge.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class CharacterWriting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WritingSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PackId = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseId = table.Column<string>(type: "TEXT", nullable: false),
                    StartKey = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveKey = table.Column<string>(type: "TEXT", nullable: true),
                    CharacterJson = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Mistakes = table.Column<int>(type: "INTEGER", nullable: false),
                    Hints = table.Column<int>(type: "INTEGER", nullable: false),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResultKey = table.Column<string>(type: "TEXT", nullable: true),
                    ResultHash = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WritingSessions_Packs_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WritingStates",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PackId = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    DueAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AssistedCompletions = table.Column<int>(type: "INTEGER", nullable: false),
                    IndependentCompletions = table.Column<int>(type: "INTEGER", nullable: false),
                    SequenceCompletions = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingStates", x => new { x.UserId, x.PackId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_WritingStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WritingSessions_ActiveKey",
                table: "WritingSessions",
                column: "ActiveKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WritingSessions_ReleaseId",
                table: "WritingSessions",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_WritingSessions_UserId_PackId_StartKey",
                table: "WritingSessions",
                columns: new[] { "UserId", "PackId", "StartKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WritingSessions");

            migrationBuilder.DropTable(
                name: "WritingStates");
        }
    }
}
