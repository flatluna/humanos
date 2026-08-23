using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionEpistemicStatusAndConfidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Confidence",
                schema: "dbo",
                table: "StructuredLearningConnection",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpistemicStatus",
                schema: "dbo",
                table: "StructuredLearningConnection",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "explicit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confidence",
                schema: "dbo",
                table: "StructuredLearningConnection");

            migrationBuilder.DropColumn(
                name: "EpistemicStatus",
                schema: "dbo",
                table: "StructuredLearningConnection");
        }
    }
}
