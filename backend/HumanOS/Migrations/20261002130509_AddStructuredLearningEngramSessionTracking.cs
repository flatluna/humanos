using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningEngramSessionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StructuredLearningEngramSessionId",
                table: "StructuredLearningEngramRuntimeUsages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StructuredLearningEngramSessions",
                columns: table => new
                {
                    StructuredLearningEngramSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastActivityDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningEngramSessions", x => x.StructuredLearningEngramSessionId);
                });

            migrationBuilder.CreateTable(
                name: "StructuredLearningEngramSessionSteps",
                columns: table => new
                {
                    StructuredLearningEngramSessionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructuredLearningEngramSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningEngramSessionSteps", x => x.StructuredLearningEngramSessionStepId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningEngramSessionSteps_StructuredLearningEngramSessions_StructuredLearningEngramSessionId",
                        column: x => x.StructuredLearningEngramSessionId,
                        principalTable: "StructuredLearningEngramSessions",
                        principalColumn: "StructuredLearningEngramSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramRuntimeUsages_StructuredLearningEngramSessionId",
                table: "StructuredLearningEngramRuntimeUsages",
                column: "StructuredLearningEngramSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramSessions_PersonId",
                table: "StructuredLearningEngramSessions",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramSessions_StructuredLearningDocumentId",
                table: "StructuredLearningEngramSessions",
                column: "StructuredLearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramSessions_StructuredLearningDocumentSectionId",
                table: "StructuredLearningEngramSessions",
                column: "StructuredLearningDocumentSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramSessionSteps_StructuredLearningEngramSessionId",
                table: "StructuredLearningEngramSessionSteps",
                column: "StructuredLearningEngramSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningEngramSessionSteps");

            migrationBuilder.DropTable(
                name: "StructuredLearningEngramSessions");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningEngramRuntimeUsages_StructuredLearningEngramSessionId",
                table: "StructuredLearningEngramRuntimeUsages");

            migrationBuilder.DropColumn(
                name: "StructuredLearningEngramSessionId",
                table: "StructuredLearningEngramRuntimeUsages");
        }
    }
}
