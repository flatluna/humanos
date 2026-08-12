using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningVersionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigSnapshotJson",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessVersion",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptHash",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigSnapshotJson",
                schema: "dbo",
                table: "StructuredLearningIllustration",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessVersion",
                schema: "dbo",
                table: "StructuredLearningIllustration",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptHash",
                schema: "dbo",
                table: "StructuredLearningIllustration",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                schema: "dbo",
                table: "StructuredLearningIllustration",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigSnapshotJson",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessVersion",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptHash",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigSnapshotJson",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessVersion",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptHash",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfigSnapshotJson",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.DropColumn(
                name: "ModelName",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.DropColumn(
                name: "ProcessVersion",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.DropColumn(
                name: "PromptHash",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.DropColumn(
                name: "ConfigSnapshotJson",
                schema: "dbo",
                table: "StructuredLearningIllustration");

            migrationBuilder.DropColumn(
                name: "ProcessVersion",
                schema: "dbo",
                table: "StructuredLearningIllustration");

            migrationBuilder.DropColumn(
                name: "PromptHash",
                schema: "dbo",
                table: "StructuredLearningIllustration");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                schema: "dbo",
                table: "StructuredLearningIllustration");

            migrationBuilder.DropColumn(
                name: "ConfigSnapshotJson",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "ModelName",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "ProcessVersion",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "PromptHash",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "ConfigSnapshotJson",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "ModelName",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "ProcessVersion",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "PromptHash",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                schema: "dbo",
                table: "StructuredLearningDocument");
        }
    }
}
