using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningHypothesisResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StructuredLearningHypothesisResponse",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningHypothesisResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuestionIndex = table.Column<int>(type: "int", nullable: false),
                    SelectedOptionIndex = table.Column<int>(type: "int", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningHypothesisResponse", x => x.StructuredLearningHypothesisResponseId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningHypothesisResponse_Section",
                        column: x => x.StructuredLearningDocumentSectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningHypothesisResponse_Person_SectionId",
                schema: "dbo",
                table: "StructuredLearningHypothesisResponse",
                columns: new[] { "PersonId", "StructuredLearningDocumentSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningHypothesisResponse_StructuredLearningDocumentSectionId",
                schema: "dbo",
                table: "StructuredLearningHypothesisResponse",
                column: "StructuredLearningDocumentSectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningHypothesisResponse",
                schema: "dbo");
        }
    }
}
