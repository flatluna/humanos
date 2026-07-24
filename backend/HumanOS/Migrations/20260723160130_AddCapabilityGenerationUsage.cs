using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityGenerationUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityGenerationUsages",
                columns: table => new
                {
                    CapabilityGenerationUsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SectionLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CachedInputTokens = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGenerationUsages", x => x.CapabilityGenerationUsageId);
                    table.ForeignKey(
                        name: "FK_CapabilityGenerationUsages_Capability_CapabilityId",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGenerationUsages_CapabilityGraphId",
                table: "CapabilityGenerationUsages",
                column: "CapabilityGraphId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGenerationUsages_CapabilityId",
                table: "CapabilityGenerationUsages",
                column: "CapabilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityGenerationUsages");
        }
    }
}
