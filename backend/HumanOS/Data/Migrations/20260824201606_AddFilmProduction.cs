using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFilmProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FilmProduction",
                schema: "dbo",
                columns: table => new
                {
                    FilmProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilmProduction", x => x.FilmProductionId);
                });

            migrationBuilder.CreateTable(
                name: "FilmScene",
                schema: "dbo",
                columns: table => new
                {
                    FilmSceneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    FilmProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DialogueText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Characters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CameraMovements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnchorImageMode = table.Column<int>(type: "int", nullable: false),
                    CustomImagePrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnchorImageStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SuggestedSceneName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GeneratedPrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalImageStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImageModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilmScene", x => x.FilmSceneId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FilmProduction_TenantId",
                schema: "dbo",
                table: "FilmProduction",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FilmScene_FilmProductionId_SortOrder",
                schema: "dbo",
                table: "FilmScene",
                columns: new[] { "FilmProductionId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FilmProduction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FilmScene",
                schema: "dbo");
        }
    }
}
