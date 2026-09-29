using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddGrowWithUsSavedSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: the 8 JobRole 'InYourRole*'/'StartInYourRole*' column adds EF
            // originally generated here were dropped from this migration — they were
            // already applied out-of-band against the DB by _add_inyourrole_cols.ps1
            // earlier, and re-adding them fails with "Column names ... specified more
            // than once". This migration now only adds the new GrowWithUsSavedSearch table.
            migrationBuilder.CreateTable(
                name: "GrowWithUsSavedSearch",
                schema: "dbo",
                columns: table => new
                {
                    GrowWithUsSavedSearchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<SqlVector<float>>(type: "vector(1536)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrowWithUsSavedSearch", x => x.GrowWithUsSavedSearchId);
                    table.ForeignKey(
                        name: "FK_GrowWithUsSavedSearch_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrowWithUsSavedSearch_PersonId_CreatedDate",
                schema: "dbo",
                table: "GrowWithUsSavedSearch",
                columns: new[] { "PersonId", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrowWithUsSavedSearch",
                schema: "dbo");
        }
    }
}
