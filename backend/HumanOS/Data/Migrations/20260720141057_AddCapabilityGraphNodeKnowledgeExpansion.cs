using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityGraphNodeKnowledgeExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityGraphNodeKnowledgeExpansions",
                columns: table => new
                {
                    CapabilityGraphNodeKnowledgeExpansionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiagramIllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphNodeKnowledgeExpansions", x => x.CapabilityGraphNodeKnowledgeExpansionId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodeKnowledgeExpansions_CapabilityGraphNodeIllustrations_DiagramIllustrationId",
                        column: x => x.DiagramIllustrationId,
                        principalTable: "CapabilityGraphNodeIllustrations",
                        principalColumn: "CapabilityGraphNodeIllustrationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodeKnowledgeExpansions_CapabilityGraphNodes_CapabilityGraphNodeId",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeKnowledgeExpansions_CapabilityGraphNodeId",
                table: "CapabilityGraphNodeKnowledgeExpansions",
                column: "CapabilityGraphNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeKnowledgeExpansions_DiagramIllustrationId",
                table: "CapabilityGraphNodeKnowledgeExpansions",
                column: "DiagramIllustrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityGraphNodeKnowledgeExpansions");
        }
    }
}
