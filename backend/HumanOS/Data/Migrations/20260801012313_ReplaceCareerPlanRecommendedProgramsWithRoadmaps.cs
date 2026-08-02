using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCareerPlanRecommendedProgramsWithRoadmaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RecommendedProgramsJson",
                schema: "dbo",
                table: "PersonCareerPlan",
                newName: "TechnicalRoadmapJson");

            migrationBuilder.AddColumn<string>(
                name: "SoftSkillsRoadmapJson",
                schema: "dbo",
                table: "PersonCareerPlan",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoftSkillsRoadmapJson",
                schema: "dbo",
                table: "PersonCareerPlan");

            migrationBuilder.RenameColumn(
                name: "TechnicalRoadmapJson",
                schema: "dbo",
                table: "PersonCareerPlan",
                newName: "RecommendedProgramsJson");
        }
    }
}
