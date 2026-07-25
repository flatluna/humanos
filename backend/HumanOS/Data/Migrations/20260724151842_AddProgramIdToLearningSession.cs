using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramIdToLearningSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "LearningSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_PersonId_CapabilityId_ProgramId",
                table: "LearningSessions",
                columns: new[] { "PersonId", "CapabilityId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_ProgramId",
                table: "LearningSessions",
                column: "ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningSessions_Program_ProgramId",
                table: "LearningSessions",
                column: "ProgramId",
                principalSchema: "dbo",
                principalTable: "Program",
                principalColumn: "ProgramId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningSessions_Program_ProgramId",
                table: "LearningSessions");

            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_PersonId_CapabilityId_ProgramId",
                table: "LearningSessions");

            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_ProgramId",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "LearningSessions");
        }
    }
}
