using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationshipClassIdToConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelationshipClassId",
                schema: "dbo",
                table: "StructuredLearningConnection",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StructuredLearningRelationshipClass",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningRelationshipClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Generic"),
                    GenericDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NormalizedLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningRelationshipClass", x => x.StructuredLearningRelationshipClassId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningRelationshipClass_TenantId_SubjectId_NormalizedLabel",
                schema: "dbo",
                table: "StructuredLearningRelationshipClass",
                columns: new[] { "TenantId", "SubjectId", "NormalizedLabel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningRelationshipClass",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "RelationshipClassId",
                schema: "dbo",
                table: "StructuredLearningConnection");
        }
    }
}
