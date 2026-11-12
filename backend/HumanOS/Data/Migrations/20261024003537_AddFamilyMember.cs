using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyMember",
                schema: "dbo",
                columns: table => new
                {
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreferredName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FamilyRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoleTitleEs = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RoleTitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    EducationLevelEs = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EducationLevelEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Institution = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PrimaryLocation = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AzureOid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AvatarBg = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "from-emerald-600 to-teal-700"),
                    LanguagesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TopFocusJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WeeklyHoursTarget = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 8.0m),
                    LearningStreakDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TargetMilestoneEs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetMilestoneEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMember", x => x.MemberId);
                    table.ForeignKey(
                        name: "FK_FamilyMember_FamilyMaster",
                        column: x => x.FamilyId,
                        principalSchema: "dbo",
                        principalTable: "FamilyMaster",
                        principalColumn: "FamilyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMember_FamilyId",
                schema: "dbo",
                table: "FamilyMember",
                column: "FamilyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyMember",
                schema: "dbo");
        }
    }
}
