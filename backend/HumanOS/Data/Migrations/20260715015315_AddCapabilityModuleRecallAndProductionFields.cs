using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityModuleRecallAndProductionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LearnerProduction",
                schema: "dbo",
                table: "CapabilityModule",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecallRequirement",
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
                name: "LearnerProduction",
                schema: "dbo",
                table: "CapabilityModule");

            migrationBuilder.DropColumn(
                name: "RecallRequirement",
                schema: "dbo",
                table: "CapabilityModule");
        }
    }
}
