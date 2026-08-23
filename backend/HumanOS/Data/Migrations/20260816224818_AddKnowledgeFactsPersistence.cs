using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeFactsPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeFacts",
                schema: "dbo",
                columns: table => new
                {
                    KnowledgeFactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    EngramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FactType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Importance = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DerivationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EpistemicStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeFacts", x => x.KnowledgeFactId);
                    table.CheckConstraint("CK_KnowledgeFacts_Confidence", "[Confidence] >= 0 AND [Confidence] <= 1");
                    table.CheckConstraint("CK_KnowledgeFacts_DerivationType", "[DerivationType] IN ('Atomic','Composite')");
                    table.CheckConstraint("CK_KnowledgeFacts_EpistemicStatus", "[EpistemicStatus] IN ('explicit','inferred')");
                    table.CheckConstraint("CK_KnowledgeFacts_FactType", "[FactType] IN ('Definition','Structure','Relationship','Principle','Capability','Process','Function','Outcome','Application')");
                    table.CheckConstraint("CK_KnowledgeFacts_Importance", "[Importance] IN ('Core','Supporting')");
                    table.CheckConstraint("CK_KnowledgeFacts_Statement_NotEmpty", "LEN(LTRIM(RTRIM([Statement]))) > 0");
                    table.ForeignKey(
                        name: "FK_KnowledgeFacts_Engram",
                        column: x => x.EngramId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeFacts_EngramId",
                schema: "dbo",
                table: "KnowledgeFacts",
                column: "EngramId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeFacts_EngramId_Importance",
                schema: "dbo",
                table: "KnowledgeFacts",
                columns: new[] { "EngramId", "Importance" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeFacts",
                schema: "dbo");
        }
    }
}
