using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnrichCapabilityGraphNodesAndIllustrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicDefinition",
                table: "CapabilityGraphNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationsJson",
                table: "CapabilityGraphNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExamplesJson",
                table: "CapabilityGraphNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Interpretation",
                table: "CapabilityGraphNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferencesJson",
                table: "CapabilityGraphNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CapabilityGraphNodeIllustrations",
                columns: table => new
                {
                    CapabilityGraphNodeIllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImageModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphNodeIllustrations", x => x.CapabilityGraphNodeIllustrationId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodeIllustrations_CapabilityGraphNodes_CapabilityGraphNodeId",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeIllustrations_CapabilityGraphNodeId",
                table: "CapabilityGraphNodeIllustrations",
                column: "CapabilityGraphNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityGraphNodeIllustrations");

            migrationBuilder.DropColumn(
                name: "AcademicDefinition",
                table: "CapabilityGraphNodes");

            migrationBuilder.DropColumn(
                name: "ApplicationsJson",
                table: "CapabilityGraphNodes");

            migrationBuilder.DropColumn(
                name: "ExamplesJson",
                table: "CapabilityGraphNodes");

            migrationBuilder.DropColumn(
                name: "Interpretation",
                table: "CapabilityGraphNodes");

            migrationBuilder.DropColumn(
                name: "ReferencesJson",
                table: "CapabilityGraphNodes");
        }
    }
}
