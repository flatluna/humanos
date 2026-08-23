using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningKnowledgeGraphToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeKnowledgeGraphConnections",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeKnowledgeGraphEntities",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeKnowledgeGraphConnections",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "IncludeKnowledgeGraphEntities",
                schema: "dbo",
                table: "StructuredLearningDocument");
        }
    }
}
