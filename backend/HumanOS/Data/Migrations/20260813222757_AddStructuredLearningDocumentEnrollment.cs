using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningDocumentEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StructuredLearningDocumentEnrollment",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningDocumentEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrolledDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningDocumentEnrollment", x => x.StructuredLearningDocumentEnrollmentId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningDocumentEnrollment_Person_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StructuredLearningDocumentEnrollment_StructuredLearningDocument_StructuredLearningDocumentId",
                        column: x => x.StructuredLearningDocumentId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocument",
                        principalColumn: "StructuredLearningDocumentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningDocumentEnrollment_StructuredLearningDocumentId",
                schema: "dbo",
                table: "StructuredLearningDocumentEnrollment",
                column: "StructuredLearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "UX_StructuredLearningDocumentEnrollment_Person_Document",
                schema: "dbo",
                table: "StructuredLearningDocumentEnrollment",
                columns: new[] { "PersonId", "StructuredLearningDocumentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningDocumentEnrollment",
                schema: "dbo");
        }
    }
}
