using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBlueprintValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlueprintValidations",
                columns: table => new
                {
                    BlueprintValidationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeExperienceBlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintValidations", x => x.BlueprintValidationId);
                    table.ForeignKey(
                        name: "FK_BlueprintValidations_NodeExperienceBlueprints_NodeExperienceBlueprintId",
                        column: x => x.NodeExperienceBlueprintId,
                        principalTable: "NodeExperienceBlueprints",
                        principalColumn: "NodeExperienceBlueprintId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintValidationIssues",
                columns: table => new
                {
                    BlueprintValidationIssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlueprintValidationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintValidationIssues", x => x.BlueprintValidationIssueId);
                    table.ForeignKey(
                        name: "FK_BlueprintValidationIssues_BlueprintValidations_BlueprintValidationId",
                        column: x => x.BlueprintValidationId,
                        principalTable: "BlueprintValidations",
                        principalColumn: "BlueprintValidationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintValidationMetrics",
                columns: table => new
                {
                    BlueprintValidationMetricId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlueprintValidationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetricValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintValidationMetrics", x => x.BlueprintValidationMetricId);
                    table.ForeignKey(
                        name: "FK_BlueprintValidationMetrics_BlueprintValidations_BlueprintValidationId",
                        column: x => x.BlueprintValidationId,
                        principalTable: "BlueprintValidations",
                        principalColumn: "BlueprintValidationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintValidationIssues_BlueprintValidationId",
                table: "BlueprintValidationIssues",
                column: "BlueprintValidationId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintValidationMetrics_BlueprintValidationId",
                table: "BlueprintValidationMetrics",
                column: "BlueprintValidationId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintValidations_NodeExperienceBlueprintId",
                table: "BlueprintValidations",
                column: "NodeExperienceBlueprintId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlueprintValidationIssues");

            migrationBuilder.DropTable(
                name: "BlueprintValidationMetrics");

            migrationBuilder.DropTable(
                name: "BlueprintValidations");
        }
    }
}
