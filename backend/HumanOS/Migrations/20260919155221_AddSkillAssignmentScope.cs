using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillAssignmentScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicableDepartmentIdsJson",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentScope",
                schema: "dbo",
                table: "Skill",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicableDepartmentIdsJson",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "AssignmentScope",
                schema: "dbo",
                table: "Skill");
        }
    }
}
