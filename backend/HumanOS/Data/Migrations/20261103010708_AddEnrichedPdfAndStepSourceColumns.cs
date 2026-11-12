using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrichedPdfAndStepSourceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceContent",
                schema: "dbo",
                table: "ProcessStep",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepImagesJson",
                schema: "dbo",
                table: "ProcessStep",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnrichedPdfFileName",
                schema: "dbo",
                table: "ProcessDocument",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnrichedPdfStoragePath",
                schema: "dbo",
                table: "ProcessDocument",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceContent",
                schema: "dbo",
                table: "ProcessStep");

            migrationBuilder.DropColumn(
                name: "StepImagesJson",
                schema: "dbo",
                table: "ProcessStep");

            migrationBuilder.DropColumn(
                name: "EnrichedPdfFileName",
                schema: "dbo",
                table: "ProcessDocument");

            migrationBuilder.DropColumn(
                name: "EnrichedPdfStoragePath",
                schema: "dbo",
                table: "ProcessDocument");
        }
    }
}
