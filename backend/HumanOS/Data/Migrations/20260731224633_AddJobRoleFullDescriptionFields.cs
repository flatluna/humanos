using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRoleFullDescriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailabilityNotes",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BenefitsJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "DailyActivitiesJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedOutcomesJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "PerformanceIndicatorsJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<Guid>(
                name: "ReportsToJobRoleId",
                schema: "dbo",
                table: "JobRole",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredCertificationsJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "RequiredEducation",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredLanguagesJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresTravel",
                schema: "dbo",
                table: "JobRole",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SalaryCurrency",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryRangeMax",
                schema: "dbo",
                table: "JobRole",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryRangeMin",
                schema: "dbo",
                table: "JobRole",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftSkillsJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "TechnicalSkillsJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ToolsJson",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "WorkLocation",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkModality",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkSchedule",
                schema: "dbo",
                table: "JobRole",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRole_ReportsToJobRoleId",
                schema: "dbo",
                table: "JobRole",
                column: "ReportsToJobRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobRole_ReportsToJobRole",
                schema: "dbo",
                table: "JobRole",
                column: "ReportsToJobRoleId",
                principalSchema: "dbo",
                principalTable: "JobRole",
                principalColumn: "JobRoleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobRole_ReportsToJobRole",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropIndex(
                name: "IX_JobRole_ReportsToJobRoleId",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "AvailabilityNotes",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "BenefitsJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "DailyActivitiesJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "ExpectedOutcomesJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "PerformanceIndicatorsJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "ReportsToJobRoleId",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "RequiredCertificationsJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "RequiredEducation",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "RequiredLanguagesJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "RequiresTravel",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "SalaryCurrency",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "SalaryRangeMax",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "SalaryRangeMin",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "SoftSkillsJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "TechnicalSkillsJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "ToolsJson",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "WorkLocation",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "WorkModality",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropColumn(
                name: "WorkSchedule",
                schema: "dbo",
                table: "JobRole");
        }
    }
}
