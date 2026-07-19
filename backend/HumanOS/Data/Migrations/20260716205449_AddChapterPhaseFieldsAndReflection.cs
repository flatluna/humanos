using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterPhaseFieldsAndReflection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CarriesProbePrediction",
                schema: "dbo",
                table: "CapabilityModuleChapter",
                newName: "IsPrimaryWeight");

            migrationBuilder.AddColumn<bool>(
                name: "IsCumulativeRecall",
                schema: "dbo",
                table: "CapabilityModuleChapter",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MiniPracticePrompt",
                schema: "dbo",
                table: "CapabilityModuleChapter",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PredictionPrompt",
                schema: "dbo",
                table: "CapabilityModuleChapter",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecallPrompt",
                schema: "dbo",
                table: "CapabilityModuleChapter",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReflectionPrompt",
                schema: "dbo",
                table: "CapabilityModule",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCumulativeRecall",
                schema: "dbo",
                table: "CapabilityModuleChapter");

            migrationBuilder.DropColumn(
                name: "MiniPracticePrompt",
                schema: "dbo",
                table: "CapabilityModuleChapter");

            migrationBuilder.DropColumn(
                name: "PredictionPrompt",
                schema: "dbo",
                table: "CapabilityModuleChapter");

            migrationBuilder.DropColumn(
                name: "RecallPrompt",
                schema: "dbo",
                table: "CapabilityModuleChapter");

            migrationBuilder.DropColumn(
                name: "ReflectionPrompt",
                schema: "dbo",
                table: "CapabilityModule");

            migrationBuilder.RenameColumn(
                name: "IsPrimaryWeight",
                schema: "dbo",
                table: "CapabilityModuleChapter",
                newName: "CarriesProbePrediction");
        }
    }
}
