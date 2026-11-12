using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessSkillRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessSkillRequirement",
                schema: "dbo",
                columns: table => new
                {
                    ProcessSkillRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsAiSkill = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AppliesToAllDepartments = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSkillRequirement", x => x.ProcessSkillRequirementId);
                    table.ForeignKey(
                        name: "FK_ProcessSkillRequirement_ProcessDocument_ProcessDocumentId",
                        column: x => x.ProcessDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ProcessDocument",
                        principalColumn: "ProcessDocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessSkillRequirementDepartment",
                schema: "dbo",
                columns: table => new
                {
                    ProcessSkillRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSkillRequirementDepartment", x => new { x.ProcessSkillRequirementId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_ProcessSkillRequirementDepartment_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "dbo",
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessSkillRequirementDepartment_ProcessSkillRequirement_ProcessSkillRequirementId",
                        column: x => x.ProcessSkillRequirementId,
                        principalSchema: "dbo",
                        principalTable: "ProcessSkillRequirement",
                        principalColumn: "ProcessSkillRequirementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessSkillRequirementJobRole",
                schema: "dbo",
                columns: table => new
                {
                    ProcessSkillRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSkillRequirementJobRole", x => new { x.ProcessSkillRequirementId, x.JobRoleId });
                    table.ForeignKey(
                        name: "FK_ProcessSkillRequirementJobRole_JobRole_JobRoleId",
                        column: x => x.JobRoleId,
                        principalSchema: "dbo",
                        principalTable: "JobRole",
                        principalColumn: "JobRoleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessSkillRequirementJobRole_ProcessSkillRequirement_ProcessSkillRequirementId",
                        column: x => x.ProcessSkillRequirementId,
                        principalSchema: "dbo",
                        principalTable: "ProcessSkillRequirement",
                        principalColumn: "ProcessSkillRequirementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSkillRequirement_ProcessDocumentId",
                schema: "dbo",
                table: "ProcessSkillRequirement",
                column: "ProcessDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSkillRequirementDepartment_DepartmentId",
                schema: "dbo",
                table: "ProcessSkillRequirementDepartment",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSkillRequirementJobRole_JobRoleId",
                schema: "dbo",
                table: "ProcessSkillRequirementJobRole",
                column: "JobRoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessSkillRequirementDepartment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProcessSkillRequirementJobRole",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProcessSkillRequirement",
                schema: "dbo");
        }
    }
}
