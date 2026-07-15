using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityModuleVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityModuleVerification",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleVerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetMetric = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecallStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecallEvidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecallEvidenceLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecallOccursBeforeInstruction = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModuleVerification", x => x.CapabilityModuleVerificationId);
                    table.ForeignKey(
                        name: "FK_CapabilityModuleVerification_CapabilityModule",
                        column: x => x.CapabilityModuleId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModule",
                        principalColumn: "CapabilityModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityModuleSuccessCriterionResult",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleSuccessCriterionResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityModuleVerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Criterion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSatisfied = table.Column<bool>(type: "bit", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModuleSuccessCriterionResult", x => x.CapabilityModuleSuccessCriterionResultId);
                    table.ForeignKey(
                        name: "FK_CapabilityModuleSuccessCriterionResult_CapabilityModuleVerification",
                        column: x => x.CapabilityModuleVerificationId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModuleVerification",
                        principalColumn: "CapabilityModuleVerificationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityModuleSuccessCriterionResult_CapabilityModuleVerificationId",
                schema: "dbo",
                table: "CapabilityModuleSuccessCriterionResult",
                column: "CapabilityModuleVerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityModuleVerification_CapabilityModuleId",
                schema: "dbo",
                table: "CapabilityModuleVerification",
                column: "CapabilityModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityModuleSuccessCriterionResult",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityModuleVerification",
                schema: "dbo");
        }
    }
}
