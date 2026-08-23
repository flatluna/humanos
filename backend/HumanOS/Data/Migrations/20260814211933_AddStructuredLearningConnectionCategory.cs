using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningConnectionCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RelationshipCategory",
                schema: "dbo",
                table: "StructuredLearningConnection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Generic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelationshipCategory",
                schema: "dbo",
                table: "StructuredLearningConnection");
        }
    }
}
