using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningEngramRuntimeUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StructuredLearningEngramRuntimeUsages",
                columns: table => new
                {
                    StructuredLearningEngramRuntimeUsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningEngramRuntimeUsages", x => x.StructuredLearningEngramRuntimeUsageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramRuntimeUsages_PersonId",
                table: "StructuredLearningEngramRuntimeUsages",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramRuntimeUsages_StructuredLearningDocumentId",
                table: "StructuredLearningEngramRuntimeUsages",
                column: "StructuredLearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEngramRuntimeUsages_StructuredLearningDocumentSectionId",
                table: "StructuredLearningEngramRuntimeUsages",
                column: "StructuredLearningDocumentSectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningEngramRuntimeUsages");
        }
    }
}
