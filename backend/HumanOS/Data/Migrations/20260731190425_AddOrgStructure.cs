using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                schema: "dbo",
                table: "Person",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobRoleId",
                schema: "dbo",
                table: "Person",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                schema: "dbo",
                table: "Person",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrgRole",
                schema: "dbo",
                table: "Person",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Department",
                schema: "dbo",
                columns: table => new
                {
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Department", x => x.DepartmentId);
                    table.ForeignKey(
                        name: "FK_Department_ParentDepartment",
                        column: x => x.ParentDepartmentId,
                        principalSchema: "dbo",
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Department_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobRole",
                schema: "dbo",
                columns: table => new
                {
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LevelLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponsibilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    RequiredExperience = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRole", x => x.JobRoleId);
                    table.ForeignKey(
                        name: "FK_JobRole_Department",
                        column: x => x.DepartmentId,
                        principalSchema: "dbo",
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobRole_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Person_DepartmentId",
                schema: "dbo",
                table: "Person",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_JobRoleId",
                schema: "dbo",
                table: "Person",
                column: "JobRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_ManagerId",
                schema: "dbo",
                table: "Person",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Department_ParentDepartmentId",
                schema: "dbo",
                table: "Department",
                column: "ParentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Department_TenantId",
                schema: "dbo",
                table: "Department",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRole_DepartmentId",
                schema: "dbo",
                table: "JobRole",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRole_TenantId",
                schema: "dbo",
                table: "JobRole",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Person_Department",
                schema: "dbo",
                table: "Person",
                column: "DepartmentId",
                principalSchema: "dbo",
                principalTable: "Department",
                principalColumn: "DepartmentId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Person_JobRole",
                schema: "dbo",
                table: "Person",
                column: "JobRoleId",
                principalSchema: "dbo",
                principalTable: "JobRole",
                principalColumn: "JobRoleId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Person_Manager",
                schema: "dbo",
                table: "Person",
                column: "ManagerId",
                principalSchema: "dbo",
                principalTable: "Person",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Person_Department",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropForeignKey(
                name: "FK_Person_JobRole",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropForeignKey(
                name: "FK_Person_Manager",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropTable(
                name: "JobRole",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Department",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Person_DepartmentId",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropIndex(
                name: "IX_Person_JobRoleId",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropIndex(
                name: "IX_Person_ManagerId",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "JobRoleId",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                schema: "dbo",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "OrgRole",
                schema: "dbo",
                table: "Person");
        }
    }
}
