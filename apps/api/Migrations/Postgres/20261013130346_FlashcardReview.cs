using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LearnForge.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class FlashcardReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlashcardSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PackId = table.Column<string>(type: "text", nullable: false),
                    ReleaseId = table.Column<string>(type: "text", nullable: false),
                    StartKey = table.Column<string>(type: "text", nullable: false),
                    FilterHash = table.Column<string>(type: "text", nullable: false),
                    ActiveKey = table.Column<string>(type: "text", nullable: true),
                    UnitsJson = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Revealed = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlashcardSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlashcardSessions_Packs_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FlashcardStates",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PackId = table.Column<string>(type: "text", nullable: false),
                    UnitId = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Introduced = table.Column<bool>(type: "boolean", nullable: false),
                    Suspended = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SchedulerVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardStates", x => new { x.UserId, x.PackId, x.UnitId });
                    table.ForeignKey(
                        name: "FK_FlashcardStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlashcardEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    RequestId = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    UnitId = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<string>(type: "text", nullable: true),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ViewJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlashcardEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlashcardEvents_FlashcardSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "FlashcardSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardEvents_SessionId_RequestId",
                table: "FlashcardEvents",
                columns: new[] { "SessionId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardEvents_UserId",
                table: "FlashcardEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardSessions_ActiveKey",
                table: "FlashcardSessions",
                column: "ActiveKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardSessions_ReleaseId",
                table: "FlashcardSessions",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardSessions_UserId_PackId_StartKey",
                table: "FlashcardSessions",
                columns: new[] { "UserId", "PackId", "StartKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlashcardEvents");

            migrationBuilder.DropTable(
                name: "FlashcardStates");

            migrationBuilder.DropTable(
                name: "FlashcardSessions");
        }
    }
}
