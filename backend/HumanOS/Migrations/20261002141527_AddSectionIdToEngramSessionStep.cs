using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionIdToEngramSessionStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StructuredLearningDocumentSectionId",
                table: "StructuredLearningEngramSessionSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramSessionSteps_StructuredLearningDocumentSectionId",
                table: "StructuredLearningEngramSessionSteps",
                column: "StructuredLearningDocumentSectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningEngramSessionSteps_StructuredLearningDocumentSectionId",
                table: "StructuredLearningEngramSessionSteps");

            migrationBuilder.DropColumn(
                name: "StructuredLearningDocumentSectionId",
                table: "StructuredLearningEngramSessionSteps");
        }
    }
}
