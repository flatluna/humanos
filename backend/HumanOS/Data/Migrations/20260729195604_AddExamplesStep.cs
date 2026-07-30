using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExamplesStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExampleCardsJson",
                table: "NodeExperienceBlueprintSteps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeExamplesStep",
                schema: "dbo",
                table: "Capability",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExampleCardsJson",
                table: "NodeExperienceBlueprintSteps");

            migrationBuilder.DropColumn(
                name: "IncludeExamplesStep",
                schema: "dbo",
                table: "Capability");
        }
    }
}
