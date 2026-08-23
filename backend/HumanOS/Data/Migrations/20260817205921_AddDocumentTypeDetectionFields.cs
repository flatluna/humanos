using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeDetectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmedDocumentType",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedDocumentType",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DetectedDocumentTypeConfidence",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DocumentTypeConfirmedByUser",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedDocumentType",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "DetectedDocumentType",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "DetectedDocumentTypeConfidence",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "DocumentTypeConfirmedByUser",
                schema: "dbo",
                table: "StructuredLearningDocument");
        }
    }
}
