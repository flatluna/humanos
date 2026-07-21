using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentQuestionIllustration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IllustrationId",
                table: "AssessmentQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentQuestions_IllustrationId",
                table: "AssessmentQuestions",
                column: "IllustrationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssessmentQuestions_CapabilityGraphNodeIllustrations_IllustrationId",
                table: "AssessmentQuestions",
                column: "IllustrationId",
                principalTable: "CapabilityGraphNodeIllustrations",
                principalColumn: "CapabilityGraphNodeIllustrationId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssessmentQuestions_CapabilityGraphNodeIllustrations_IllustrationId",
                table: "AssessmentQuestions");

            migrationBuilder.DropIndex(
                name: "IX_AssessmentQuestions_IllustrationId",
                table: "AssessmentQuestions");

            migrationBuilder.DropColumn(
                name: "IllustrationId",
                table: "AssessmentQuestions");
        }
    }
}
