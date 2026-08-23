using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityClassIdToStructuredLearningEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EntityClassId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntity_EntityClassId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                column: "EntityClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_StructuredLearningEntity_EntityClass",
                schema: "dbo",
                table: "StructuredLearningEntity",
                column: "EntityClassId",
                principalSchema: "dbo",
                principalTable: "StructuredLearningEntityClass",
                principalColumn: "StructuredLearningEntityClassId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StructuredLearningEntity_EntityClass",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningEntity_EntityClassId",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropColumn(
                name: "EntityClassId",
                schema: "dbo",
                table: "StructuredLearningEntity");
        }
    }
}
