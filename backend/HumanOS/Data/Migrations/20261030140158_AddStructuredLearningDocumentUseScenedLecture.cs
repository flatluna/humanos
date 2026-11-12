using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningDocumentUseScenedLecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default TRUE (2026-10-30) so documents created before this field
            // existed keep their current scened-lecture behavior unchanged —
            // the Studio "new course" form explicitly sends false for brand
            // new documents instead of relying on this column default.
            migrationBuilder.AddColumn<bool>(
                name: "UseScenedLecture",
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
                name: "UseScenedLecture",
                schema: "dbo",
                table: "StructuredLearningDocument");
        }
    }
}
