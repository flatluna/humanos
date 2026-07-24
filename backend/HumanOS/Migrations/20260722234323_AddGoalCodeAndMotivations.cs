using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalCodeAndMotivations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "Goal",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Motivation",
                schema: "dbo",
                columns: table => new
                {
                    MotivationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Motivation", x => x.MotivationId);
                });

            migrationBuilder.CreateTable(
                name: "MotivationTranslation",
                schema: "dbo",
                columns: table => new
                {
                    MotivationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivationTranslation", x => new { x.MotivationId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_MotivationTranslation_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MotivationTranslation_Motivation",
                        column: x => x.MotivationId,
                        principalSchema: "dbo",
                        principalTable: "Motivation",
                        principalColumn: "MotivationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonMotivation",
                schema: "dbo",
                columns: table => new
                {
                    PersonMotivationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MotivationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonMotivation", x => x.PersonMotivationId);
                    table.ForeignKey(
                        name: "FK_PersonMotivation_Motivation",
                        column: x => x.MotivationId,
                        principalSchema: "dbo",
                        principalTable: "Motivation",
                        principalColumn: "MotivationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonMotivation_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Goal_Code",
                schema: "dbo",
                table: "Goal",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Motivation_Code",
                schema: "dbo",
                table: "Motivation",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MotivationTranslation_LanguageCode",
                schema: "dbo",
                table: "MotivationTranslation",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_PersonMotivation_MotivationId",
                schema: "dbo",
                table: "PersonMotivation",
                column: "MotivationId");

            migrationBuilder.CreateIndex(
                name: "UX_PersonMotivation_PersonId_MotivationId",
                schema: "dbo",
                table: "PersonMotivation",
                columns: new[] { "PersonId", "MotivationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MotivationTranslation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PersonMotivation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Motivation",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UX_Goal_Code",
                schema: "dbo",
                table: "Goal");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "dbo",
                table: "Goal");
        }
    }
}
