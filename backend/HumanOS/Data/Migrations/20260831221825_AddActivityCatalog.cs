using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobRole_Department",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.AddColumn<string>(
                name: "DocumentFileName",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentStoragePath",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                schema: "dbo",
                table: "JobRole",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "dbo",
                table: "Department",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "Department",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "dbo",
                table: "Department",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostCenter",
                schema: "dbo",
                table: "Department",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaderPersonId",
                schema: "dbo",
                table: "Department",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "dbo",
                table: "Department",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MissionStatement",
                schema: "dbo",
                table: "Department",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Activity",
                schema: "dbo",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activity", x => x.ActivityId);
                    table.ForeignKey(
                        name: "FK_Activity_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentJobRole",
                schema: "dbo",
                columns: table => new
                {
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentJobRole", x => new { x.DepartmentId, x.JobRoleId });
                    table.ForeignKey(
                        name: "FK_DepartmentJobRole_Department",
                        column: x => x.DepartmentId,
                        principalSchema: "dbo",
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentJobRole_JobRole",
                        column: x => x.JobRoleId,
                        principalSchema: "dbo",
                        principalTable: "JobRole",
                        principalColumn: "JobRoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRoleActivity",
                schema: "dbo",
                columns: table => new
                {
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRoleActivity", x => new { x.JobRoleId, x.ActivityId });
                    table.ForeignKey(
                        name: "FK_JobRoleActivity_Activity",
                        column: x => x.ActivityId,
                        principalSchema: "dbo",
                        principalTable: "Activity",
                        principalColumn: "ActivityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobRoleActivity_JobRole",
                        column: x => x.JobRoleId,
                        principalSchema: "dbo",
                        principalTable: "JobRole",
                        principalColumn: "JobRoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Department_LeaderPersonId",
                schema: "dbo",
                table: "Department",
                column: "LeaderPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Activity_TenantId",
                schema: "dbo",
                table: "Activity",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Activity_TenantId_Name",
                schema: "dbo",
                table: "Activity",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentJobRole_JobRoleId",
                schema: "dbo",
                table: "DepartmentJobRole",
                column: "JobRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRoleActivity_ActivityId",
                schema: "dbo",
                table: "JobRoleActivity",
                column: "ActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Department_LeaderPerson",
                schema: "dbo",
                table: "Department",
                column: "LeaderPersonId",
                principalSchema: "dbo",
                principalTable: "Person",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JobRole_Department",
                schema: "dbo",
                table: "JobRole",
                column: "DepartmentId",
                principalSchema: "dbo",
                principalTable: "Department",
                principalColumn: "DepartmentId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Department_LeaderPerson",
                schema: "dbo",
                table: "Department");

            migrationBuilder.DropForeignKey(
                name: "FK_JobRole_Department",
                schema: "dbo",
                table: "JobRole");

            migrationBuilder.DropTable(
                name: "DepartmentJobRole",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "JobRoleActivity",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Activity",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Department_LeaderPersonId",
                schema: "dbo",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "DocumentFileName",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "DocumentStoragePath",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "dbo",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "dbo",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "dbo",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "CostCenter",
                schema: "dbo",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "LeaderPersonId",
                schema: "dbo",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "dbo",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "MissionStatement",
                schema: "dbo",
                table: "Department");

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                schema: "dbo",
                table: "JobRole",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_JobRole_Department",
                schema: "dbo",
                table: "JobRole",
                column: "DepartmentId",
                principalSchema: "dbo",
                principalTable: "Department",
                principalColumn: "DepartmentId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
