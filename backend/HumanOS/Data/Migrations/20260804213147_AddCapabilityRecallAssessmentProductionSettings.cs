using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityRecallAssessmentProductionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssessmentQuestionsPerRound",
                schema: "dbo",
                table: "Capability",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeProductionStep",
                schema: "dbo",
                table: "Capability",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "RecallItemsRequired",
                schema: "dbo",
                table: "Capability",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssessmentQuestionsPerRound",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "IncludeProductionStep",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "RecallItemsRequired",
                schema: "dbo",
                table: "Capability");
        }
    }
}
