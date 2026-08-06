using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerPersonId",
                schema: "dbo",
                table: "Capability",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerTenantId",
                schema: "dbo",
                table: "Capability",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Capability_OwnerPersonId",
                schema: "dbo",
                table: "Capability",
                column: "OwnerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Capability_OwnerTenantId",
                schema: "dbo",
                table: "Capability",
                column: "OwnerTenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Capability_OwnerPerson",
                schema: "dbo",
                table: "Capability",
                column: "OwnerPersonId",
                principalSchema: "dbo",
                principalTable: "Person",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Capability_OwnerTenant",
                schema: "dbo",
                table: "Capability",
                column: "OwnerTenantId",
                principalSchema: "dbo",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Capability_OwnerPerson",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropForeignKey(
                name: "FK_Capability_OwnerTenant",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropIndex(
                name: "IX_Capability_OwnerPersonId",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropIndex(
                name: "IX_Capability_OwnerTenantId",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "OwnerPersonId",
                schema: "dbo",
                table: "Capability");

            migrationBuilder.DropColumn(
                name: "OwnerTenantId",
                schema: "dbo",
                table: "Capability");
        }
    }
}
