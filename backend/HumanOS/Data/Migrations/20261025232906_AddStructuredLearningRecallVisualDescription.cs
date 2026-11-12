using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningRecallVisualDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentRecallVisualObjectsDescription",
                schema: "dbo",
                table: "StructuredLearningSectionStep",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentRecallVisualObjectsDescription",
                schema: "dbo",
                table: "StructuredLearningPersonSectionState",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentRecallVisualObjectsDescription",
                schema: "dbo",
                table: "StructuredLearningSectionStep");

            migrationBuilder.DropColumn(
                name: "CurrentRecallVisualObjectsDescription",
                schema: "dbo",
                table: "StructuredLearningPersonSectionState");
        }
    }
}
