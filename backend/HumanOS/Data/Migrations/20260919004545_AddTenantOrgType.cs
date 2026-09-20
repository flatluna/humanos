using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantOrgType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrgType",
                schema: "dbo",
                table: "Tenant",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Corporate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrgType",
                schema: "dbo",
                table: "Tenant");
        }
    }
}
