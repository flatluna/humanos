using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningPersonRuntimeState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PersonId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StructuredLearningPersonSectionState",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningPersonSectionStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentRecallPrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentDeepDiveCardIndex = table.Column<int>(type: "int", nullable: false),
                    CurrentDeepDivePrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningPersonSectionState", x => x.StructuredLearningPersonSectionStateId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningPersonSectionState_Section",
                        column: x => x.StructuredLearningDocumentSectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningRecallAttempt_Person_SectionId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt",
                columns: new[] { "PersonId", "StructuredLearningDocumentSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningAssessmentRound_Person_SectionId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound",
                columns: new[] { "PersonId", "StructuredLearningDocumentSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningPersonSectionState_StructuredLearningDocumentSectionId",
                schema: "dbo",
                table: "StructuredLearningPersonSectionState",
                column: "StructuredLearningDocumentSectionId");

            migrationBuilder.CreateIndex(
                name: "UX_StructuredLearningPersonSectionState_Person_SectionId",
                schema: "dbo",
                table: "StructuredLearningPersonSectionState",
                columns: new[] { "PersonId", "StructuredLearningDocumentSectionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningPersonSectionState",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningRecallAttempt_Person_SectionId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningAssessmentRound_Person_SectionId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound");
        }
    }
}
