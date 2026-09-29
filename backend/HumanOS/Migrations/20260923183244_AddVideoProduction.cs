using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoProduction",
                schema: "dbo",
                columns: table => new
                {
                    VideoProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FinalVideoStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoProduction", x => x.VideoProductionId);
                });

            migrationBuilder.CreateTable(
                name: "VideoProductionClip",
                schema: "dbo",
                columns: table => new
                {
                    VideoProductionClipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    VideoProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VideoModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoProductionClip", x => x.VideoProductionClipId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoProduction_TenantId",
                schema: "dbo",
                table: "VideoProduction",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoProductionClip_VideoProductionId_SortOrder",
                schema: "dbo",
                table: "VideoProductionClip",
                columns: new[] { "VideoProductionId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoProduction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "VideoProductionClip",
                schema: "dbo");
        }
    }
}
