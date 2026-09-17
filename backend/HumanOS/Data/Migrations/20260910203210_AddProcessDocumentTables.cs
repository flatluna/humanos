using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessDocumentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessDocument",
                schema: "dbo",
                columns: table => new
                {
                    ProcessDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "1.0"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StorageContainer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageBlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    TotalStepsCount = table.Column<int>(type: "int", nullable: false),
                    UniqueSkillsCount = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessDocument", x => x.ProcessDocumentId);
                    table.ForeignKey(
                        name: "FK_ProcessDocument_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "dbo",
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProcessDocument_JobRole_JobRoleId",
                        column: x => x.JobRoleId,
                        principalSchema: "dbo",
                        principalTable: "JobRole",
                        principalColumn: "JobRoleId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProcessDocument_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessChapter",
                schema: "dbo",
                columns: table => new
                {
                    ProcessChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChapterNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessChapter", x => x.ProcessChapterId);
                    table.ForeignKey(
                        name: "FK_ProcessChapter_ProcessDocument_ProcessDocumentId",
                        column: x => x.ProcessDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ProcessDocument",
                        principalColumn: "ProcessDocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessStep",
                schema: "dbo",
                columns: table => new
                {
                    ProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StepNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActivitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObservableAbilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiredToolsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliverablesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletionCondition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourcePagesJson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsExcluded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStep", x => x.ProcessStepId);
                    table.ForeignKey(
                        name: "FK_ProcessStep_ProcessChapter_ProcessChapterId",
                        column: x => x.ProcessChapterId,
                        principalSchema: "dbo",
                        principalTable: "ProcessChapter",
                        principalColumn: "ProcessChapterId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessStep_ProcessDocument_ProcessDocumentId",
                        column: x => x.ProcessDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ProcessDocument",
                        principalColumn: "ProcessDocumentId");
                });

            migrationBuilder.CreateTable(
                name: "ProcessStepSkill",
                schema: "dbo",
                columns: table => new
                {
                    ProcessStepSkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SkillName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsAiSkill = table.Column<bool>(type: "bit", nullable: false),
                    Criticality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "critical"),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverageStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "new_skill_required"),
                    MatchedCourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedCourseName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepSkill", x => x.ProcessStepSkillId);
                    table.ForeignKey(
                        name: "FK_ProcessStepSkill_ProcessStep_ProcessStepId",
                        column: x => x.ProcessStepId,
                        principalSchema: "dbo",
                        principalTable: "ProcessStep",
                        principalColumn: "ProcessStepId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessStepSkill_Skill_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "dbo",
                        principalTable: "Skill",
                        principalColumn: "SkillId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessChapter_ProcessDocumentId",
                schema: "dbo",
                table: "ProcessChapter",
                column: "ProcessDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDocument_DepartmentId",
                schema: "dbo",
                table: "ProcessDocument",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDocument_JobRoleId",
                schema: "dbo",
                table: "ProcessDocument",
                column: "JobRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDocument_TenantId",
                schema: "dbo",
                table: "ProcessDocument",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStep_ProcessChapterId",
                schema: "dbo",
                table: "ProcessStep",
                column: "ProcessChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStep_ProcessDocumentId",
                schema: "dbo",
                table: "ProcessStep",
                column: "ProcessDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepSkill_ProcessStepId",
                schema: "dbo",
                table: "ProcessStepSkill",
                column: "ProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepSkill_SkillId",
                schema: "dbo",
                table: "ProcessStepSkill",
                column: "SkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessStepSkill",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProcessStep",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProcessChapter",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProcessDocument",
                schema: "dbo");
        }
    }
}
