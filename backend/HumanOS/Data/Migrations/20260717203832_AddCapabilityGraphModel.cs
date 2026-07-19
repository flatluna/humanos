using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityGraphModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityGraphs",
                columns: table => new
                {
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphs", x => x.CapabilityGraphId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphs_Capability_CapabilityId",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityGraphNodes",
                columns: table => new
                {
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NodeType = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphNodes", x => x.CapabilityGraphNodeId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodes_CapabilityGraphs_CapabilityGraphId",
                        column: x => x.CapabilityGraphId,
                        principalTable: "CapabilityGraphs",
                        principalColumn: "CapabilityGraphId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityGraphEdges",
                columns: table => new
                {
                    CapabilityGraphEdgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphEdges", x => x.CapabilityGraphEdgeId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphEdges_CapabilityGraphNodes_SourceNodeId",
                        column: x => x.SourceNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId");
                    table.ForeignKey(
                        name: "FK_CapabilityGraphEdges_CapabilityGraphNodes_TargetNodeId",
                        column: x => x.TargetNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId");
                    table.ForeignKey(
                        name: "FK_CapabilityGraphEdges_CapabilityGraphs_CapabilityGraphId",
                        column: x => x.CapabilityGraphId,
                        principalTable: "CapabilityGraphs",
                        principalColumn: "CapabilityGraphId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphEdges_CapabilityGraphId",
                table: "CapabilityGraphEdges",
                column: "CapabilityGraphId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphEdges_SourceNodeId_TargetNodeId",
                table: "CapabilityGraphEdges",
                columns: new[] { "SourceNodeId", "TargetNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphEdges_TargetNodeId",
                table: "CapabilityGraphEdges",
                column: "TargetNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodes_CapabilityGraphId_Name",
                table: "CapabilityGraphNodes",
                columns: new[] { "CapabilityGraphId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodes_CapabilityGraphId_SortOrder",
                table: "CapabilityGraphNodes",
                columns: new[] { "CapabilityGraphId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphs_CapabilityId",
                table: "CapabilityGraphs",
                column: "CapabilityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityGraphEdges");

            migrationBuilder.DropTable(
                name: "CapabilityGraphNodes");

            migrationBuilder.DropTable(
                name: "CapabilityGraphs");
        }
    }
}
