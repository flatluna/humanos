using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameStepDataJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExampleCardsJson",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                newName: "StepDataJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StepDataJson",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                newName: "ExampleCardsJson");
        }
    }
}
