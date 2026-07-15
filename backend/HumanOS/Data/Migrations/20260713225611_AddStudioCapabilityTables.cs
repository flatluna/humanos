using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// This is the FIRST EF Core migration ever generated for this project
    /// (Data/Migrations was empty before). Every pre-existing table
    /// (Tenant, Person, Capability, CapabilityDomain, etc.) was created
    /// outside of EF Migrations, so this migration was hand-trimmed down
    /// to ONLY the 4 new Human OS Studio tables (CapabilityLevel,
    /// CapabilityModule, CapabilityModuleMetric, CapabilityKnowledgeChunk)
    /// — the auto-generated version tried to re-CreateTable every existing
    /// table too, which would fail against the real database. See
    /// /memories/repo/humanstudio-multiagent-vision.md.
    /// </remarks>
    public partial class AddStudioCapabilityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityLevel",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Layer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    HumanTransformation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityLevel", x => x.CapabilityLevelId);
                    table.ForeignKey(
                        name: "FK_CapabilityLevel_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityModule",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Script = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetricRationale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModule", x => x.CapabilityModuleId);
                    table.ForeignKey(
                        name: "FK_CapabilityModule_CapabilityLevel",
                        column: x => x.CapabilityLevelId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityLevel",
                        principalColumn: "CapabilityLevelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityKnowledgeChunk",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityKnowledgeChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<SqlVector<float>>(type: "vector(1536)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityKnowledgeChunk", x => x.CapabilityKnowledgeChunkId);
                    table.ForeignKey(
                        name: "FK_CapabilityKnowledgeChunk_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapabilityKnowledgeChunk_CapabilityModule",
                        column: x => x.CapabilityModuleId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModule",
                        principalColumn: "CapabilityModuleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityModuleMetric",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModuleMetric", x => new { x.CapabilityModuleId, x.Metric });
                    table.ForeignKey(
                        name: "FK_CapabilityModuleMetric_CapabilityModule",
                        column: x => x.CapabilityModuleId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModule",
                        principalColumn: "CapabilityModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityKnowledgeChunk_CapabilityId",
                schema: "dbo",
                table: "CapabilityKnowledgeChunk",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityKnowledgeChunk_CapabilityModuleId",
                schema: "dbo",
                table: "CapabilityKnowledgeChunk",
                column: "CapabilityModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityLevel_CapabilityId",
                schema: "dbo",
                table: "CapabilityLevel",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityModule_CapabilityLevelId",
                schema: "dbo",
                table: "CapabilityModule",
                column: "CapabilityLevelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityModuleMetric",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityKnowledgeChunk",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityModule",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityLevel",
                schema: "dbo");
        }
    }
}
