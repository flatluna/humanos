using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityLegalConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsentAcceptedByName",
                schema: "dbo",
                table: "Capability",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsentAcceptedByPersonId",
                schema: "dbo",
                table: "Capability",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentAcceptedDate",
                schema: "dbo",
                table: "Capability",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Capability_ConsentAcceptedByPersonId",
                schema: "dbo",
                table: "Capability",
                column: "ConsentAcceptedByPersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Capability_ConsentAcceptedByPerson",
                schema: "dbo",
                table: "Capability",
                column: "ConsentAcceptedByPersonId",
                principalSchema: "dbo",
                principalTable: "Person",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Capability_ConsentAcceptedByPerson",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropIndex(
                name: "IX_Capability_ConsentAcceptedByPersonId",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "ConsentAcceptedByName",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "ConsentAcceptedByPersonId",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "ConsentAcceptedDate",
                schema: "dbo",
                table: "Capability");
        }
    }
}
