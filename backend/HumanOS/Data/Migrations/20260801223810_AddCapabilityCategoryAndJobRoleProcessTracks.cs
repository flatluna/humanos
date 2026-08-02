using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityCategoryAndJobRoleProcessTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepartmentProcessesRoadmapJson",
                schema: "dbo",
                table: "PersonCareerPlan",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "HrProcessesRoadmapJson",
                schema: "dbo",
                table: "PersonCareerPlan",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentProcessesJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "HrProcessesJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "dbo",
                table: "Capability",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetJobRoleId",
                schema: "dbo",
                table: "Capability",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetSkill",
                schema: "dbo",
                table: "Capability",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Capability_Category",
                schema: "dbo",
                table: "Capability",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Capability_TargetJobRoleId",
                schema: "dbo",
                table: "Capability",
                column: "TargetJobRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Capability_TargetJobRole",
                schema: "dbo",
                table: "Capability",
                column: "TargetJobRoleId",
                principalSchema: "dbo",
                principalTable: "JobRole",
                principalColumn: "JobRoleId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Capability_TargetJobRole",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropIndex(
                name: "IX_Capability_Category",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropIndex(
                name: "IX_Capability_TargetJobRoleId",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "DepartmentProcessesRoadmapJson",
                schema: "dbo",
                table: "PersonCareerPlan");

            migrationBuilder.DropColumn(
                name: "HrProcessesRoadmapJson",
                schema: "dbo",
                table: "PersonCareerPlan");

            migrationBuilder.DropColumn(
                name: "DepartmentProcessesJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "HrProcessesJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "TargetJobRoleId",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "TargetSkill",
                schema: "dbo",
                table: "Capability");
        }
    }
}
