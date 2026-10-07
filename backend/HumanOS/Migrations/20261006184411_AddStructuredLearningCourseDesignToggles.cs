using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningCourseDesignToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StaticAssessmentQuestionsJson",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaticRecallItemsJson",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssessmentGenerationMode",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Dynamic");

            migrationBuilder.AddColumn<bool>(
                name: "IncludeWorkplaceApplicationStep",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "RecallGenerationMode",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Dynamic");

            migrationBuilder.AddColumn<bool>(
                name: "ShowDeepDiveAgentChat",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowQaProfessorStep",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaticAssessmentQuestionsJson",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "StaticRecallItemsJson",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "AssessmentGenerationMode",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "IncludeWorkplaceApplicationStep",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "RecallGenerationMode",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "ShowDeepDiveAgentChat",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "ShowQaProfessorStep",
                schema: "dbo",
                table: "StructuredLearningDocument");
        }
    }
}
