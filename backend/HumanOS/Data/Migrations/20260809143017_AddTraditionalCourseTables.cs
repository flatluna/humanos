using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTraditionalCourseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LearningMode",
                schema: "dbo",
                table: "Capability",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CapabilityChapters",
                columns: table => new
                {
                    CapabilityChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityChapters", x => x.CapabilityChapterId);
                    table.ForeignKey(
                        name: "FK_CapabilityChapters_Capability_CapabilityId",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityTraditionalIllustrations",
                columns: table => new
                {
                    CapabilityTraditionalIllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    ImageModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityTraditionalIllustrations", x => x.CapabilityTraditionalIllustrationId);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityLessons",
                columns: table => new
                {
                    CapabilityLessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityLessons", x => x.CapabilityLessonId);
                    table.ForeignKey(
                        name: "FK_CapabilityLessons_CapabilityChapters_CapabilityChapterId",
                        column: x => x.CapabilityChapterId,
                        principalTable: "CapabilityChapters",
                        principalColumn: "CapabilityChapterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityChapterExamQuestions",
                columns: table => new
                {
                    CapabilityChapterExamQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectOptionIndex = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CapabilityTraditionalIllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityChapterExamQuestions", x => x.CapabilityChapterExamQuestionId);
                    table.ForeignKey(
                        name: "FK_CapabilityChapterExamQuestions_CapabilityChapters_CapabilityChapterId",
                        column: x => x.CapabilityChapterId,
                        principalTable: "CapabilityChapters",
                        principalColumn: "CapabilityChapterId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapabilityChapterExamQuestions_CapabilityTraditionalIllustrations_CapabilityTraditionalIllustrationId",
                        column: x => x.CapabilityTraditionalIllustrationId,
                        principalTable: "CapabilityTraditionalIllustrations",
                        principalColumn: "CapabilityTraditionalIllustrationId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityLessonCards",
                columns: table => new
                {
                    CapabilityLessonCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityLessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CapabilityTraditionalIllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityLessonCards", x => x.CapabilityLessonCardId);
                    table.ForeignKey(
                        name: "FK_CapabilityLessonCards_CapabilityLessons_CapabilityLessonId",
                        column: x => x.CapabilityLessonId,
                        principalTable: "CapabilityLessons",
                        principalColumn: "CapabilityLessonId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapabilityLessonCards_CapabilityTraditionalIllustrations_CapabilityTraditionalIllustrationId",
                        column: x => x.CapabilityTraditionalIllustrationId,
                        principalTable: "CapabilityTraditionalIllustrations",
                        principalColumn: "CapabilityTraditionalIllustrationId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityChapterExamQuestions_CapabilityChapterId",
                table: "CapabilityChapterExamQuestions",
                column: "CapabilityChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityChapterExamQuestions_CapabilityTraditionalIllustrationId",
                table: "CapabilityChapterExamQuestions",
                column: "CapabilityTraditionalIllustrationId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityChapters_CapabilityId",
                table: "CapabilityChapters",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityLessonCards_CapabilityLessonId",
                table: "CapabilityLessonCards",
                column: "CapabilityLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityLessonCards_CapabilityTraditionalIllustrationId",
                table: "CapabilityLessonCards",
                column: "CapabilityTraditionalIllustrationId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityLessons_CapabilityChapterId",
                table: "CapabilityLessons",
                column: "CapabilityChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityTraditionalIllustrations_CapabilityId",
                table: "CapabilityTraditionalIllustrations",
                column: "CapabilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityChapterExamQuestions");

            migrationBuilder.DropTable(
                name: "CapabilityLessonCards");

            migrationBuilder.DropTable(
                name: "CapabilityLessons");

            migrationBuilder.DropTable(
                name: "CapabilityTraditionalIllustrations");

            migrationBuilder.DropTable(
                name: "CapabilityChapters");

            migrationBuilder.DropColumn(
                name: "LearningMode",
                schema: "dbo",
                table: "Capability");
        }
    }
}
