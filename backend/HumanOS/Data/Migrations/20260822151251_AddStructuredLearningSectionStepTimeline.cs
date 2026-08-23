using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningSectionStepTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimelineJson",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MediaType",
                schema: "dbo",
                table: "StructuredLearningIllustration",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Image",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "StructuredLearningVideoGenerationLogs",
                columns: table => new
                {
                    StructuredLearningVideoGenerationLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningVideoGenerationLogs", x => x.StructuredLearningVideoGenerationLogId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningVideoGenerationLogs");

            migrationBuilder.DropColumn(
                name: "TimelineJson",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.AlterColumn<string>(
                name: "MediaType",
                schema: "dbo",
                table: "StructuredLearningIllustration",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Image");
        }
    }
}
