using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeepDiveStepProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentDeepDiveCardIndex",
                table: "LearningSessionSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrentDeepDiveQuestion",
                table: "LearningSessionSteps",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentDeepDiveCardIndex",
                table: "LearningSessionSteps");

            migrationBuilder.DropColumn(
                name: "CurrentDeepDiveQuestion",
                table: "LearningSessionSteps");
        }
    }
}
