using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessDocumentScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppliesToAllDepartments",
                schema: "dbo",
                table: "ProcessDocument",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProcessDocumentDepartment",
                schema: "dbo",
                columns: table => new
                {
                    ProcessDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessDocumentDepartment", x => new { x.ProcessDocumentId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_ProcessDocumentDepartment_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "dbo",
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessDocumentDepartment_ProcessDocument_ProcessDocumentId",
                        column: x => x.ProcessDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ProcessDocument",
                        principalColumn: "ProcessDocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessDocumentJobRole",
                schema: "dbo",
                columns: table => new
                {
                    ProcessDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessDocumentJobRole", x => new { x.ProcessDocumentId, x.JobRoleId });
                    table.ForeignKey(
                        name: "FK_ProcessDocumentJobRole_JobRole_JobRoleId",
                        column: x => x.JobRoleId,
                        principalSchema: "dbo",
                        principalTable: "JobRole",
                        principalColumn: "JobRoleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessDocumentJobRole_ProcessDocument_ProcessDocumentId",
                        column: x => x.ProcessDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ProcessDocument",
                        principalColumn: "ProcessDocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDocumentDepartment_DepartmentId",
                schema: "dbo",
                table: "ProcessDocumentDepartment",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDocumentJobRole_JobRoleId",
                schema: "dbo",
                table: "ProcessDocumentJobRole",
                column: "JobRoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessDocumentDepartment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProcessDocumentJobRole",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "AppliesToAllDepartments",
                schema: "dbo",
                table: "ProcessDocument");
        }
    }
}
