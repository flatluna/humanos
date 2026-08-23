using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityResolutionAndSubjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MatchConfidence",
                schema: "dbo",
                table: "StructuredLearningEntitySection",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchReason",
                schema: "dbo",
                table: "StructuredLearningEntitySection",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionStatus",
                schema: "dbo",
                table: "StructuredLearningEntitySection",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "new");

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntity_TenantId_SubjectId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                columns: new[] { "TenantId", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningEntity_TenantId_SubjectId",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropColumn(
                name: "MatchConfidence",
                schema: "dbo",
                table: "StructuredLearningEntitySection");

            migrationBuilder.DropColumn(
                name: "MatchReason",
                schema: "dbo",
                table: "StructuredLearningEntitySection");

            migrationBuilder.DropColumn(
                name: "ResolutionStatus",
                schema: "dbo",
                table: "StructuredLearningEntitySection");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                schema: "dbo",
                table: "StructuredLearningEntity");
        }
    }
}
