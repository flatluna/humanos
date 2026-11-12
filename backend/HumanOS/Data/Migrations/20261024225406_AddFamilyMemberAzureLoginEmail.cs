using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyMemberAzureLoginEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AzureLinkedDate",
                schema: "dbo",
                table: "FamilyMember",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureLoginEmail",
                schema: "dbo",
                table: "FamilyMember",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AzureLinkedDate",
                schema: "dbo",
                table: "FamilyMember");

            migrationBuilder.DropColumn(
                name: "AzureLoginEmail",
                schema: "dbo",
                table: "FamilyMember");
        }
    }
}
