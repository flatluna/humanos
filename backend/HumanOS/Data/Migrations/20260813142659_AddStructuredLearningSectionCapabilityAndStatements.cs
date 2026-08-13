using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningSectionCapabilityAndStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capability",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Statements",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capability",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "Statements",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");
        }
    }
}
