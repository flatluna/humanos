using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddTecPracticeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PracticeSession",
                schema: "dbo",
                columns: table => new
                {
                    PracticeSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExercisesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSession", x => x.PracticeSessionId);
                });

            migrationBuilder.CreateTable(
                name: "PracticeAttempt",
                schema: "dbo",
                columns: table => new
                {
                    PracticeAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PracticeSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseIndex = table.Column<int>(type: "int", nullable: false),
                    StudentAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PointsAwarded = table.Column<int>(type: "int", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeAttempt", x => x.PracticeAttemptId);
                    table.ForeignKey(
                        name: "FK_PracticeAttempt_PracticeSession",
                        column: x => x.PracticeSessionId,
                        principalSchema: "dbo",
                        principalTable: "PracticeSession",
                        principalColumn: "PracticeSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeAttempt_PracticeSessionId_ExerciseIndex",
                schema: "dbo",
                table: "PracticeAttempt",
                columns: new[] { "PracticeSessionId", "ExerciseIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSession_PersonId_CapabilityId",
                schema: "dbo",
                table: "PracticeSession",
                columns: new[] { "PersonId", "CapabilityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PracticeAttempt",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PracticeSession",
                schema: "dbo");
        }
    }
}
