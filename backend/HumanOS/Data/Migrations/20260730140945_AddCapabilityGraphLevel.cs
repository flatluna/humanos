using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityGraphLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityGraphLevels",
                columns: table => new
                {
                    CapabilityGraphLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LevelIndex = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphLevels", x => x.CapabilityGraphLevelId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphLevels_CapabilityGraphs_CapabilityGraphId",
                        column: x => x.CapabilityGraphId,
                        principalTable: "CapabilityGraphs",
                        principalColumn: "CapabilityGraphId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphLevels_CapabilityGraphId_LevelIndex",
                table: "CapabilityGraphLevels",
                columns: new[] { "CapabilityGraphId", "LevelIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityGraphLevels");
        }
    }
}
