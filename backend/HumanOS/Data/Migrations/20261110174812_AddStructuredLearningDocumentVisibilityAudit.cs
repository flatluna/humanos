using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningDocumentVisibilityAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StructuredLearningDocumentVisibilityAudit",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningDocumentVisibilityAuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldIsGlobal = table.Column<bool>(type: "bit", nullable: false),
                    NewIsGlobal = table.Column<bool>(type: "bit", nullable: false),
                    OldIsPublished = table.Column<bool>(type: "bit", nullable: false),
                    NewIsPublished = table.Column<bool>(type: "bit", nullable: false),
                    ChangedByOid = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChangedByEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ChangedByTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningDocumentVisibilityAudit", x => x.StructuredLearningDocumentVisibilityAuditId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningDocumentVisibilityAudit_StructuredLearningDocumentId_ChangedAtUtc",
                schema: "dbo",
                table: "StructuredLearningDocumentVisibilityAudit",
                columns: new[] { "StructuredLearningDocumentId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningDocumentVisibilityAudit",
                schema: "dbo");
        }
    }
}
