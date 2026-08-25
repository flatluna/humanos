using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFilmProductionVideoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FinalVideoStoragePath",
                schema: "dbo",
                table: "FilmScene",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoErrorMessage",
                schema: "dbo",
                table: "FilmScene",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoModel",
                schema: "dbo",
                table: "FilmScene",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VideoStatus",
                schema: "dbo",
                table: "FilmScene",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FinalFilmStoragePath",
                schema: "dbo",
                table: "FilmProduction",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalVideoStoragePath",
                schema: "dbo",
                table: "FilmScene");

            migrationBuilder.DropColumn(
                name: "VideoErrorMessage",
                schema: "dbo",
                table: "FilmScene");

            migrationBuilder.DropColumn(
                name: "VideoModel",
                schema: "dbo",
                table: "FilmScene");

            migrationBuilder.DropColumn(
                name: "VideoStatus",
                schema: "dbo",
                table: "FilmScene");

            migrationBuilder.DropColumn(
                name: "FinalFilmStoragePath",
                schema: "dbo",
                table: "FilmProduction");
        }
    }
}
