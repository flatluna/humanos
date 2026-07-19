using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningSessionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningSessions",
                columns: table => new
                {
                    LearningSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessions", x => x.LearningSessionId);
                    table.ForeignKey(
                        name: "FK_LearningSessions_Capability_CapabilityId",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningSessions_Person_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningSessionNodes",
                columns: table => new
                {
                    LearningSessionNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeExperienceBlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessionNodes", x => x.LearningSessionNodeId);
                    table.ForeignKey(
                        name: "FK_LearningSessionNodes_CapabilityGraphNodes_CapabilityGraphNodeId",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningSessionNodes_LearningSessions_LearningSessionId",
                        column: x => x.LearningSessionId,
                        principalTable: "LearningSessions",
                        principalColumn: "LearningSessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningSessionNodes_NodeExperienceBlueprints_NodeExperienceBlueprintId",
                        column: x => x.NodeExperienceBlueprintId,
                        principalTable: "NodeExperienceBlueprints",
                        principalColumn: "NodeExperienceBlueprintId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningAssessmentResults",
                columns: table => new
                {
                    LearningAssessmentResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAssessmentResults", x => x.LearningAssessmentResultId);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentResults_LearningSessionNodes_LearningSessionNodeId",
                        column: x => x.LearningSessionNodeId,
                        principalTable: "LearningSessionNodes",
                        principalColumn: "LearningSessionNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningSessionSteps",
                columns: table => new
                {
                    LearningSessionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessionSteps", x => x.LearningSessionStepId);
                    table.ForeignKey(
                        name: "FK_LearningSessionSteps_LearningSessionNodes_LearningSessionNodeId",
                        column: x => x.LearningSessionNodeId,
                        principalTable: "LearningSessionNodes",
                        principalColumn: "LearningSessionNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningEvidences",
                columns: table => new
                {
                    LearningEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentResponse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningEvidences", x => x.LearningEvidenceId);
                    table.ForeignKey(
                        name: "FK_LearningEvidences_LearningSessionSteps_LearningSessionStepId",
                        column: x => x.LearningSessionStepId,
                        principalTable: "LearningSessionSteps",
                        principalColumn: "LearningSessionStepId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentResults_LearningSessionNodeId",
                table: "LearningAssessmentResults",
                column: "LearningSessionNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningEvidences_LearningSessionStepId",
                table: "LearningEvidences",
                column: "LearningSessionStepId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionNodes_CapabilityGraphNodeId",
                table: "LearningSessionNodes",
                column: "CapabilityGraphNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionNodes_LearningSessionId",
                table: "LearningSessionNodes",
                column: "LearningSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionNodes_NodeExperienceBlueprintId",
                table: "LearningSessionNodes",
                column: "NodeExperienceBlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_CapabilityId",
                table: "LearningSessions",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_PersonId",
                table: "LearningSessions",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_PersonId_CapabilityId",
                table: "LearningSessions",
                columns: new[] { "PersonId", "CapabilityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionSteps_LearningSessionNodeId_StepType",
                table: "LearningSessionSteps",
                columns: new[] { "LearningSessionNodeId", "StepType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningAssessmentResults");

            migrationBuilder.DropTable(
                name: "LearningEvidences");

            migrationBuilder.DropTable(
                name: "LearningSessionSteps");

            migrationBuilder.DropTable(
                name: "LearningSessionNodes");

            migrationBuilder.DropTable(
                name: "LearningSessions");
        }
    }
}
