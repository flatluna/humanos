using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExperienceRequirementCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExperienceRequirement",
                schema: "dbo",
                columns: table => new
                {
                    ExperienceRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperienceRequirement", x => x.ExperienceRequirementId);
                    table.ForeignKey(
                        name: "FK_ExperienceRequirement_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRoleExperienceRequirement",
                schema: "dbo",
                columns: table => new
                {
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExperienceRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRoleExperienceRequirement", x => new { x.JobRoleId, x.ExperienceRequirementId });
                    table.ForeignKey(
                        name: "FK_JobRoleExperienceRequirement_ExperienceRequirement",
                        column: x => x.ExperienceRequirementId,
                        principalSchema: "dbo",
                        principalTable: "ExperienceRequirement",
                        principalColumn: "ExperienceRequirementId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobRoleExperienceRequirement_JobRole",
                        column: x => x.JobRoleId,
                        principalSchema: "dbo",
                        principalTable: "JobRole",
                        principalColumn: "JobRoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRequirement_TenantId",
                schema: "dbo",
                table: "ExperienceRequirement",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRequirement_TenantId_Name",
                schema: "dbo",
                table: "ExperienceRequirement",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_JobRoleExperienceRequirement_ExperienceRequirementId",
                schema: "dbo",
                table: "JobRoleExperienceRequirement",
                column: "ExperienceRequirementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRoleExperienceRequirement",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ExperienceRequirement",
                schema: "dbo");
        }
    }
}
