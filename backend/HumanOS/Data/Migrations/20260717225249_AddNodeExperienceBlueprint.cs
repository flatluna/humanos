using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNodeExperienceBlueprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NodeExperienceBlueprints",
                columns: table => new
                {
                    NodeExperienceBlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeExperienceBlueprints", x => x.NodeExperienceBlueprintId);
                    table.ForeignKey(
                        name: "FK_NodeExperienceBlueprints_CapabilityGraphNodes_CapabilityGraphNodeId",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NodeExperienceBlueprintSteps",
                columns: table => new
                {
                    NodeExperienceBlueprintStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeExperienceBlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepType = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferencedIllustrationIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeExperienceBlueprintSteps", x => x.NodeExperienceBlueprintStepId);
                    table.ForeignKey(
                        name: "FK_NodeExperienceBlueprintSteps_NodeExperienceBlueprints_NodeExperienceBlueprintId",
                        column: x => x.NodeExperienceBlueprintId,
                        principalTable: "NodeExperienceBlueprints",
                        principalColumn: "NodeExperienceBlueprintId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NodeExperienceBlueprints_CapabilityGraphNodeId",
                table: "NodeExperienceBlueprints",
                column: "CapabilityGraphNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeExperienceBlueprints_CapabilityGraphNodeId_Name_Version",
                table: "NodeExperienceBlueprints",
                columns: new[] { "CapabilityGraphNodeId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodeExperienceBlueprintSteps_NodeExperienceBlueprintId_SortOrder",
                table: "NodeExperienceBlueprintSteps",
                columns: new[] { "NodeExperienceBlueprintId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeExperienceBlueprintSteps_NodeExperienceBlueprintId_StepType",
                table: "NodeExperienceBlueprintSteps",
                columns: new[] { "NodeExperienceBlueprintId", "StepType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeExperienceBlueprintSteps");

            migrationBuilder.DropTable(
                name: "NodeExperienceBlueprints");
        }
    }
}
