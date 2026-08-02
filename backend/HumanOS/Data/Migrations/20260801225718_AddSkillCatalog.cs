using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Skill",
                schema: "dbo",
                columns: table => new
                {
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skill", x => x.SkillId);
                });

            migrationBuilder.CreateTable(
                name: "JobRoleSkill",
                schema: "dbo",
                columns: table => new
                {
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRoleSkill", x => new { x.JobRoleId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_JobRoleSkill_JobRole",
                        column: x => x.JobRoleId,
                        principalSchema: "dbo",
                        principalTable: "JobRole",
                        principalColumn: "JobRoleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobRoleSkill_Skill",
                        column: x => x.SkillId,
                        principalSchema: "dbo",
                        principalTable: "Skill",
                        principalColumn: "SkillId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobRoleSkill_SkillId",
                schema: "dbo",
                table: "JobRoleSkill",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skill_TenantId_Category",
                schema: "dbo",
                table: "Skill",
                columns: new[] { "TenantId", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRoleSkill",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Skill",
                schema: "dbo");
        }
    }
}
