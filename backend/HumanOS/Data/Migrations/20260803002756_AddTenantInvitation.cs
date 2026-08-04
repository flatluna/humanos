using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantInvitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantInvitation",
                schema: "dbo",
                columns: table => new
                {
                    TenantInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgRole = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AcceptedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInvitation", x => x.TenantInvitationId);
                    table.ForeignKey(
                        name: "FK_TenantInvitation_Department",
                        column: x => x.DepartmentId,
                        principalSchema: "dbo",
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TenantInvitation_JobRole",
                        column: x => x.JobRoleId,
                        principalSchema: "dbo",
                        principalTable: "JobRole",
                        principalColumn: "JobRoleId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TenantInvitation_Manager",
                        column: x => x.ManagerId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TenantInvitation_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitation_DepartmentId",
                schema: "dbo",
                table: "TenantInvitation",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitation_JobRoleId",
                schema: "dbo",
                table: "TenantInvitation",
                column: "JobRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitation_ManagerId",
                schema: "dbo",
                table: "TenantInvitation",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitation_Tenant_NormalizedEmail_Status",
                schema: "dbo",
                table: "TenantInvitation",
                columns: new[] { "TenantId", "NormalizedEmail", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitation_TenantId",
                schema: "dbo",
                table: "TenantInvitation",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantInvitation",
                schema: "dbo");
        }
    }
}
