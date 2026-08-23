using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningDocumentCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmail",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByOid",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByEmail",
                schema: "dbo",
                table: "StructuredLearningDocument");

            migrationBuilder.DropColumn(
                name: "CreatedByOid",
                schema: "dbo",
                table: "StructuredLearningDocument");
        }
    }
}
