using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFilmProductionCostTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContentCachedInputTokens",
                schema: "dbo",
                table: "FilmScene",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContentInputTokens",
                schema: "dbo",
                table: "FilmScene",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentModelName",
                schema: "dbo",
                table: "FilmScene",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentOutputTokens",
                schema: "dbo",
                table: "FilmScene",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BreakdownCachedInputTokens",
                schema: "dbo",
                table: "FilmProduction",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BreakdownInputTokens",
                schema: "dbo",
                table: "FilmProduction",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BreakdownModelName",
                schema: "dbo",
                table: "FilmProduction",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BreakdownOutputTokens",
                schema: "dbo",
                table: "FilmProduction",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentCachedInputTokens",
                schema: "dbo",
                table: "FilmScene");

            migrationBuilder.DropColumn(
                name: "ContentInputTokens",
                schema: "dbo",
                table: "FilmScene");

            migrationBuilder.DropColumn(
                name: "ContentModelName",
                schema: "dbo",
                table: "FilmScene");

            migrationBuilder.DropColumn(
                name: "ContentOutputTokens",
                schema: "dbo",
                table: "FilmScene");

            migrationBuilder.DropColumn(
                name: "BreakdownCachedInputTokens",
                schema: "dbo",
                table: "FilmProduction");

            migrationBuilder.DropColumn(
                name: "BreakdownInputTokens",
                schema: "dbo",
                table: "FilmProduction");

            migrationBuilder.DropColumn(
                name: "BreakdownModelName",
                schema: "dbo",
                table: "FilmProduction");

            migrationBuilder.DropColumn(
                name: "BreakdownOutputTokens",
                schema: "dbo",
                table: "FilmProduction");
        }
    }
}
