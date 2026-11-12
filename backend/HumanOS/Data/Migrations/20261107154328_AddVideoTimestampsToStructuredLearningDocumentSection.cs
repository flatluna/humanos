using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoTimestampsToStructuredLearningDocumentSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "VideoEndSeconds",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VideoStartSeconds",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoEndSeconds",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "VideoStartSeconds",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");
        }
    }
}
