using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningPedagogyConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PedagogyErrorMessage",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PedagogyStatus",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<string>(
                name: "AssessmentQuestionType",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MultipleChoice");

            migrationBuilder.AddColumn<int>(
                name: "AssessmentQuestionsPerRound",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeProductionStep",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeRecallStep",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDeepDiveCardsPerNode",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PedagogicalAudience",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecallItemsRequired",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StructuredLearningSectionStep",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningSectionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExampleCardsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningSectionStep", x => x.StructuredLearningSectionStepId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningSectionStep_Section",
                        column: x => x.StructuredLearningDocumentSectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningSectionStep_SectionId",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                column: "StructuredLearningDocumentSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningSectionStep_TenantId",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningSectionStep",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "PedagogyErrorMessage",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "PedagogyStatus",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "AssessmentQuestionType",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "AssessmentQuestionsPerRound",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "IncludeProductionStep",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "IncludeRecallStep",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "MaxDeepDiveCardsPerNode",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "PedagogicalAudience",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "RecallItemsRequired",
                schema: "dbo",
                table: "StructuredLearningDocument");
        }
    }
}
