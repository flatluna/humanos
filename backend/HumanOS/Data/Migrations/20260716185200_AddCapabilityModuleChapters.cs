using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityModuleChapters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityModuleChapter",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TeachingContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CarriesProbePrediction = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModuleChapter", x => x.CapabilityModuleChapterId);
                    table.ForeignKey(
                        name: "FK_CapabilityModuleChapter_CapabilityModule",
                        column: x => x.CapabilityModuleId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModule",
                        principalColumn: "CapabilityModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityModuleChapter_CapabilityModuleId",
                schema: "dbo",
                table: "CapabilityModuleChapter",
                column: "CapabilityModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityModuleChapter",
                schema: "dbo");
        }
    }
}
