using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoProductionStartingImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnchorImageStoragePath",
                schema: "dbo",
                table: "VideoProductionClip",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageEditPrompt",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageMode",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePrompt",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartingImageContentType",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartingImageStoragePath",
                schema: "dbo",
                table: "VideoProduction",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnchorImageStoragePath",
                schema: "dbo",
                table: "VideoProductionClip");

            migrationBuilder.DropColumn(
                name: "ImageEditPrompt",
                schema: "dbo",
                table: "VideoProduction");

            migrationBuilder.DropColumn(
                name: "ImageMode",
                schema: "dbo",
                table: "VideoProduction");

            migrationBuilder.DropColumn(
                name: "ImagePrompt",
                schema: "dbo",
                table: "VideoProduction");

            migrationBuilder.DropColumn(
                name: "StartingImageContentType",
                schema: "dbo",
                table: "VideoProduction");

            migrationBuilder.DropColumn(
                name: "StartingImageStoragePath",
                schema: "dbo",
                table: "VideoProduction");
        }
    }
}
