using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningRealtimeRecallAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentRecallPrompt",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StructuredLearningAssessmentRound",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningAssessmentRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FinalScore = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningAssessmentRound", x => x.StructuredLearningAssessmentRoundId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningAssessmentRound_Section",
                        column: x => x.StructuredLearningDocumentSectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StructuredLearningRecallAttempt",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningRecallAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TutorPromptShown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentResponse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecallScore = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningRecallAttempt", x => x.StructuredLearningRecallAttemptId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningRecallAttempt_Section",
                        column: x => x.StructuredLearningDocumentSectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StructuredLearningAssessmentQuestionAttempt",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningAssessmentQuestionAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningAssessmentRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionIndex = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrectOptionIndex = table.Column<int>(type: "int", nullable: true),
                    StudentAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Correctness = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScoreContribution = table.Column<int>(type: "int", nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObservedError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AnsweredDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningAssessmentQuestionAttempt", x => x.StructuredLearningAssessmentQuestionAttemptId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningAssessmentQuestionAttempt_Round",
                        column: x => x.StructuredLearningAssessmentRoundId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningAssessmentRound",
                        principalColumn: "StructuredLearningAssessmentRoundId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningAssessmentQuestionAttempt_RoundId",
                schema: "dbo",
                table: "StructuredLearningAssessmentQuestionAttempt",
                column: "StructuredLearningAssessmentRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningAssessmentRound_SectionId",
                schema: "dbo",
                table: "StructuredLearningAssessmentRound",
                column: "StructuredLearningDocumentSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningRecallAttempt_SectionId",
                schema: "dbo",
                table: "StructuredLearningRecallAttempt",
                column: "StructuredLearningDocumentSectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningAssessmentQuestionAttempt",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StructuredLearningRecallAttempt",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StructuredLearningAssessmentRound",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "CurrentRecallPrompt",
                schema: "dbo",
                table: "StructuredLearningSectionStep");
        }
    }
}
