using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonAcademicPreferencesFreeText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Frustrations",
                schema: "dbo",
                table: "PersonAcademicPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportantValues",
                schema: "dbo",
                table: "PersonAcademicPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MainChallenges",
                schema: "dbo",
                table: "PersonAcademicPreferences",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Frustrations",
                schema: "dbo",
                table: "PersonAcademicPreferences");

            migrationBuilder.DropColumn(
                name: "ImportantValues",
                schema: "dbo",
                table: "PersonAcademicPreferences");

            migrationBuilder.DropColumn(
                name: "MainChallenges",
                schema: "dbo",
                table: "PersonAcademicPreferences");
        }
    }
}
