using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningGenerationUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StructuredLearningGenerationUsage",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningGenerationUsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StepName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AgentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CachedInputTokens = table.Column<int>(type: "int", nullable: false),
                    ElapsedMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningGenerationUsage", x => x.StructuredLearningGenerationUsageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningGenerationUsage_DocumentId",
                schema: "dbo",
                table: "StructuredLearningGenerationUsage",
                column: "StructuredLearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningGenerationUsage_SectionId",
                schema: "dbo",
                table: "StructuredLearningGenerationUsage",
                column: "StructuredLearningDocumentSectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningGenerationUsage",
                schema: "dbo");
        }
    }
}
