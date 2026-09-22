using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonCqiProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicableDepartmentIdsJson",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.CreateTable(
                name: "PersonCqiProfile",
                schema: "dbo",
                columns: table => new
                {
                    PersonCqiProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadingPatternType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeadingPatternName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    SourceFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExtractionModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtractedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonCqiProfile", x => x.PersonCqiProfileId);
                    table.ForeignKey(
                        name: "FK_PersonCqiProfile_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PersonCqiProfile_PersonId",
                schema: "dbo",
                table: "PersonCqiProfile",
                column: "PersonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonCqiProfile",
                schema: "dbo");

            migrationBuilder.AddColumn<string>(
                name: "ApplicableDepartmentIdsJson",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }
    }
}
