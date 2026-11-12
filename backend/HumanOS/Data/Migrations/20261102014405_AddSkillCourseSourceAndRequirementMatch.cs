using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillCourseSourceAndRequirementMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceCourseProvider",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCourseTitle",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCourseUrl",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceOutlineJson",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceOutlineSource",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MatchedSkillId",
                schema: "dbo",
                table: "ProcessSkillRequirement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSkillRequirement_MatchedSkillId",
                schema: "dbo",
                table: "ProcessSkillRequirement",
                column: "MatchedSkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessSkillRequirement_Skill_MatchedSkillId",
                schema: "dbo",
                table: "ProcessSkillRequirement",
                column: "MatchedSkillId",
                principalSchema: "dbo",
                principalTable: "Skill",
                principalColumn: "SkillId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessSkillRequirement_Skill_MatchedSkillId",
                schema: "dbo",
                table: "ProcessSkillRequirement");

            migrationBuilder.DropIndex(
                name: "IX_ProcessSkillRequirement_MatchedSkillId",
                schema: "dbo",
                table: "ProcessSkillRequirement");

            migrationBuilder.DropColumn(
                name: "SourceCourseProvider",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "SourceCourseTitle",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "SourceCourseUrl",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "SourceOutlineJson",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "SourceOutlineSource",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "MatchedSkillId",
                schema: "dbo",
                table: "ProcessSkillRequirement");
        }
    }
}
