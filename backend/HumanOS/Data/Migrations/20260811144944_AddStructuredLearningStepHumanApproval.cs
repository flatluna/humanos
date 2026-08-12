using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningStepHumanApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HumanApprovedAtUtc",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHumanApproved",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HumanApprovedAtUtc",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.DropColumn(
                name: "IsHumanApproved",
                schema: "dbo",
                table: "StructuredLearningSectionStep");
        }
    }
}
