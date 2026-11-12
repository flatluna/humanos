using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillProcessDocumentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceProcessDocumentId",
                schema: "dbo",
                table: "Skill",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CapabilitySkillId",
                schema: "dbo",
                table: "ProcessDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skill_SourceProcessDocumentId",
                schema: "dbo",
                table: "Skill",
                column: "SourceProcessDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDocument_CapabilitySkillId",
                schema: "dbo",
                table: "ProcessDocument",
                column: "CapabilitySkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessDocument_Skill_CapabilitySkillId",
                schema: "dbo",
                table: "ProcessDocument",
                column: "CapabilitySkillId",
                principalSchema: "dbo",
                principalTable: "Skill",
                principalColumn: "SkillId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Skill_ProcessDocument_SourceProcessDocumentId",
                schema: "dbo",
                table: "Skill",
                column: "SourceProcessDocumentId",
                principalSchema: "dbo",
                principalTable: "ProcessDocument",
                principalColumn: "ProcessDocumentId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessDocument_Skill_CapabilitySkillId",
                schema: "dbo",
                table: "ProcessDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_Skill_ProcessDocument_SourceProcessDocumentId",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropIndex(
                name: "IX_Skill_SourceProcessDocumentId",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropIndex(
                name: "IX_ProcessDocument_CapabilitySkillId",
                schema: "dbo",
                table: "ProcessDocument");

            migrationBuilder.DropColumn(
                name: "SourceProcessDocumentId",
                schema: "dbo",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "CapabilitySkillId",
                schema: "dbo",
                table: "ProcessDocument");
        }
    }
}
