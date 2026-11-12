using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyMemberPlatformRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlatformRole",
                schema: "dbo",
                table: "FamilyMember",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            // Demo data fixup: Jorge Luna (Familia Luna) is the EngramsIQ
            // owner/staff account used for testing — grant him AdminEngrams
            // so he can use the AI agents and HumanOS Studio.
            migrationBuilder.Sql(
                "UPDATE [dbo].[FamilyMember] SET [PlatformRole] = 'AdminEngrams' WHERE [MemberId] = '7db22824-c5b5-41c0-a5f0-e2999800866e'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlatformRole",
                schema: "dbo",
                table: "FamilyMember");
        }
    }
}
