using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeFactGraphPathSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeFactGraphPathSteps",
                schema: "dbo",
                columns: table => new
                {
                    KnowledgeFactGraphPathStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    KnowledgeFactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeFactGraphPathSteps", x => x.KnowledgeFactGraphPathStepId);
                    table.CheckConstraint("CK_KnowledgeFactGraphPathSteps_StepOrder", "[StepOrder] >= 1 AND [StepOrder] <= 3");
                    table.ForeignKey(
                        name: "FK_KnowledgeFactGraphPathSteps_Connection",
                        column: x => x.ConnectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningConnection",
                        principalColumn: "StructuredLearningConnectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeFactGraphPathSteps_KnowledgeFacts",
                        column: x => x.KnowledgeFactId,
                        principalSchema: "dbo",
                        principalTable: "KnowledgeFacts",
                        principalColumn: "KnowledgeFactId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeFactGraphPathSteps_ConnectionId",
                schema: "dbo",
                table: "KnowledgeFactGraphPathSteps",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "UX_KnowledgeFactGraphPathSteps_KnowledgeFactId_ConnectionId",
                schema: "dbo",
                table: "KnowledgeFactGraphPathSteps",
                columns: new[] { "KnowledgeFactId", "ConnectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_KnowledgeFactGraphPathSteps_KnowledgeFactId_StepOrder",
                schema: "dbo",
                table: "KnowledgeFactGraphPathSteps",
                columns: new[] { "KnowledgeFactId", "StepOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeFactGraphPathSteps",
                schema: "dbo");
        }
    }
}
