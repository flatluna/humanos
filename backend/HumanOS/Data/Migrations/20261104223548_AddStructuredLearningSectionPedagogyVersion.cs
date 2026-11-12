using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningSectionPedagogyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PedagogyGeneratedAt",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PedagogyVersion",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PedagogyGeneratedAt",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");

            migrationBuilder.DropColumn(
                name: "PedagogyVersion",
                schema: "dbo",
                table: "StructuredLearningDocumentSection");
        }
    }
}
