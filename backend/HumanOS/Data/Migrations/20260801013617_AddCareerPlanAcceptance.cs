using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerPlanAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedDate",
                schema: "dbo",
                table: "PersonCareerPlan",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAccepted",
                schema: "dbo",
                table: "PersonCareerPlan",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedDate",
                schema: "dbo",
                table: "PersonCareerPlan");

            migrationBuilder.DropColumn(
                name: "IsAccepted",
                schema: "dbo",
                table: "PersonCareerPlan");
        }
    }
}
