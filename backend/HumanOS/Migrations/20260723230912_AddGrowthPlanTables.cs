using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddGrowthPlanTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonCurrentSituation",
                schema: "dbo",
                columns: table => new
                {
                    PersonCurrentSituationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedSubjectCodes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfAssessedLevelsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonCurrentSituation", x => x.PersonCurrentSituationId);
                    table.ForeignKey(
                        name: "FK_PersonCurrentSituation_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonFutureDirection",
                schema: "dbo",
                columns: table => new
                {
                    PersonFutureDirectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedGoalIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedMotivationCodes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonFutureDirection", x => x.PersonFutureDirectionId);
                    table.ForeignKey(
                        name: "FK_PersonFutureDirection_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonStartingPoint",
                schema: "dbo",
                columns: table => new
                {
                    PersonStartingPointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedCapabilityIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GapCapabilitiesBySubjectJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonStartingPoint", x => x.PersonStartingPointId);
                    table.ForeignKey(
                        name: "FK_PersonStartingPoint_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PersonCurrentSituation_PersonId",
                schema: "dbo",
                table: "PersonCurrentSituation",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PersonFutureDirection_PersonId",
                schema: "dbo",
                table: "PersonFutureDirection",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PersonStartingPoint_PersonId",
                schema: "dbo",
                table: "PersonStartingPoint",
                column: "PersonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonCurrentSituation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PersonFutureDirection",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PersonStartingPoint",
                schema: "dbo");
        }
    }
}
