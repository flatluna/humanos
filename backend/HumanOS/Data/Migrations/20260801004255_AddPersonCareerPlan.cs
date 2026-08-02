using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonCareerPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonCareerPlan",
                schema: "dbo",
                columns: table => new
                {
                    PersonCareerPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GapSkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    RecommendedProgramsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    OverallRationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonCareerPlan", x => x.PersonCareerPlanId);
                    table.ForeignKey(
                        name: "FK_PersonCareerPlan_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PersonCareerPlan_PersonId",
                schema: "dbo",
                table: "PersonCareerPlan",
                column: "PersonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonCareerPlan",
                schema: "dbo");
        }
    }
}
