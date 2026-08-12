using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningDocumentProgramMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StructuredLearningDocumentProgramMembership",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningDocumentProgramMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningDocumentProgramMembership", x => x.StructuredLearningDocumentProgramMembershipId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningDocumentProgramMembership_Document",
                        column: x => x.StructuredLearningDocumentId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocument",
                        principalColumn: "StructuredLearningDocumentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StructuredLearningDocumentProgramMembership_Program",
                        column: x => x.ProgramId,
                        principalSchema: "dbo",
                        principalTable: "Program",
                        principalColumn: "ProgramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningDocumentProgramMembership_StructuredLearningDocumentId",
                schema: "dbo",
                table: "StructuredLearningDocumentProgramMembership",
                column: "StructuredLearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "UX_StructuredLearningDocumentProgramMembership_Program_Document",
                schema: "dbo",
                table: "StructuredLearningDocumentProgramMembership",
                columns: new[] { "ProgramId", "StructuredLearningDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StructuredLearningDocumentProgramMembership_Program_SortOrder",
                schema: "dbo",
                table: "StructuredLearningDocumentProgramMembership",
                columns: new[] { "ProgramId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningDocumentProgramMembership",
                schema: "dbo");
        }
    }
}
