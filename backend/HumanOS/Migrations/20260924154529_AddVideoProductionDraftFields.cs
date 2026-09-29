using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoProductionDraftFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Environment",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxClips",
                schema: "dbo",
                table: "VideoProduction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Objective",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptsJson",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Environment",
                schema: "dbo",
                table: "VideoProduction");

            migrationBuilder.DropColumn(
                name: "Instructions",
                schema: "dbo",
                table: "VideoProduction");

            migrationBuilder.DropColumn(
                name: "MaxClips",
                schema: "dbo",
                table: "VideoProduction");

            migrationBuilder.DropColumn(
                name: "Objective",
                schema: "dbo",
                table: "VideoProduction");

            migrationBuilder.DropColumn(
                name: "PromptsJson",
                schema: "dbo",
                table: "VideoProduction");
        }
    }
}
