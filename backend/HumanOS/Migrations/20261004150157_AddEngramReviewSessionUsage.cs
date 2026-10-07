using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddEngramReviewSessionUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EngramReviewSessionUsages",
                columns: table => new
                {
                    EngramReviewSessionUsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngramReviewSessionUsages", x => x.EngramReviewSessionUsageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EngramReviewSessionUsages_PersonId",
                table: "EngramReviewSessionUsages",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EngramReviewSessionUsages");
        }
    }
}
