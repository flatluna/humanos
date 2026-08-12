using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningDeepDiveProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentDeepDiveCardIndex",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrentDeepDivePrompt",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentDeepDiveCardIndex",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.DropColumn(
                name: "CurrentDeepDivePrompt",
                schema: "dbo",
                table: "StructuredLearningSectionStep");
        }
    }
}
