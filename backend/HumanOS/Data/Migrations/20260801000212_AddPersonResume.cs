using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonResume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonResume",
                schema: "dbo",
                columns: table => new
                {
                    PersonResumeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProfessionalObjective = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProfessionalSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    WorkExperienceJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    EducationJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    CertificationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    LanguagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    RawResumeText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtractionModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtractedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonResume", x => x.PersonResumeId);
                    table.ForeignKey(
                        name: "FK_PersonResume_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PersonResume_PersonId",
                schema: "dbo",
                table: "PersonResume",
                column: "PersonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonResume",
                schema: "dbo");
        }
    }
}
