using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionIdToRecallAndAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StructuredLearningEngramSessionId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StructuredLearningEngramSessionId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningRecallAttempt_SessionId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt",
                column: "StructuredLearningEngramSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningAssessmentRound_SessionId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound",
                column: "StructuredLearningEngramSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningRecallAttempt_SessionId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningAssessmentRound_SessionId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound");

            migrationBuilder.DropColumn(
                name: "StructuredLearningEngramSessionId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt");

            migrationBuilder.DropColumn(
                name: "StructuredLearningEngramSessionId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound");
        }
    }
}
