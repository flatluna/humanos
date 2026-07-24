using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramCapabilityObjectivesRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Objectives",
                schema: "dbo",
                table: "ProgramCapability",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requirements",
                schema: "dbo",
                table: "ProgramCapability",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Objectives",
                schema: "dbo",
                table: "ProgramCapability");

            migrationBuilder.DropColumn(
                name: "Requirements",
                schema: "dbo",
                table: "ProgramCapability");
        }
    }
}
