using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonAcademicPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonAcademicPreferences",
                schema: "dbo",
                columns: table => new
                {
                    PersonAcademicPreferencesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicInterestsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    LearningPreferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    AcademicMotivationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    GrowthAreasJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonAcademicPreferences", x => x.PersonAcademicPreferencesId);
                    table.ForeignKey(
                        name: "FK_PersonAcademicPreferences_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PersonAcademicPreferences_PersonId",
                schema: "dbo",
                table: "PersonAcademicPreferences",
                column: "PersonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonAcademicPreferences",
                schema: "dbo");
        }
    }
}
