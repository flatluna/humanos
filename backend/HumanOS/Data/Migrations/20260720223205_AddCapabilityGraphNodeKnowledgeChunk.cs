using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityGraphNodeKnowledgeChunk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityGraphNodeKnowledgeChunk",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityGraphNodeKnowledgeChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceField = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<SqlVector<float>>(type: "vector(1536)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphNodeKnowledgeChunk", x => x.CapabilityGraphNodeKnowledgeChunkId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphNode",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphId",
                schema: "dbo",
                table: "CapabilityGraphNodeKnowledgeChunk",
                column: "CapabilityGraphId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphNodeId",
                schema: "dbo",
                table: "CapabilityGraphNodeKnowledgeChunk",
                column: "CapabilityGraphNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityGraphNodeKnowledgeChunk",
                schema: "dbo");
        }
    }
}
