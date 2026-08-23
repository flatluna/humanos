using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningKnowledgeGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StructuredLearningEntity_Document",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropForeignKey(
                name: "FK_StructuredLearningEntity_PrimarySection",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningEntity_DocumentId",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningEntity_PrimarySectionId",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningEntity_TenantId",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropColumn(
                name: "PrimarySectionId",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropColumn(
                name: "StructuredLearningDocumentId",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.AddColumn<string>(
                name: "Aliases",
                schema: "dbo",
                table: "StructuredLearningEntity",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<SqlVector<float>>(
                name: "Embedding",
                schema: "dbo",
                table: "StructuredLearningEntity",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedValue",
                schema: "dbo",
                table: "StructuredLearningEntity",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "StructuredLearningEntitySection",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningEntitySectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartPosition = table.Column<int>(type: "int", nullable: true),
                    EndPosition = table.Column<int>(type: "int", nullable: true),
                    RelevanceScore = table.Column<double>(type: "float", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningEntitySection", x => x.StructuredLearningEntitySectionId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningEntitySection_Entity",
                        column: x => x.StructuredLearningEntityId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningEntity",
                        principalColumn: "StructuredLearningEntityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StructuredLearningEntitySection_Section",
                        column: x => x.StructuredLearningDocumentSectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntity_TenantId_NormalizedValue_EntityType",
                schema: "dbo",
                table: "StructuredLearningEntity",
                columns: new[] { "TenantId", "NormalizedValue", "EntityType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntitySection_EntityId_SectionId",
                schema: "dbo",
                table: "StructuredLearningEntitySection",
                columns: new[] { "StructuredLearningEntityId", "StructuredLearningDocumentSectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntitySection_SectionId",
                schema: "dbo",
                table: "StructuredLearningEntitySection",
                column: "StructuredLearningDocumentSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntitySection_TenantId",
                schema: "dbo",
                table: "StructuredLearningEntitySection",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningEntitySection",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_StructuredLearningEntity_TenantId_NormalizedValue_EntityType",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropColumn(
                name: "Aliases",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropColumn(
                name: "Embedding",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropColumn(
                name: "NormalizedValue",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.AddColumn<Guid>(
                name: "PrimarySectionId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StructuredLearningDocumentId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntity_DocumentId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                column: "StructuredLearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntity_PrimarySectionId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                column: "PrimarySectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningEntity_TenantId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_StructuredLearningEntity_Document",
                schema: "dbo",
                table: "StructuredLearningEntity",
                column: "StructuredLearningDocumentId",
                principalSchema: "dbo",
                principalTable: "StructuredLearningDocument",
                principalColumn: "StructuredLearningDocumentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StructuredLearningEntity_PrimarySection",
                schema: "dbo",
                table: "StructuredLearningEntity",
                column: "PrimarySectionId",
                principalSchema: "dbo",
                principalTable: "StructuredLearningDocumentSection",
                principalColumn: "StructuredLearningDocumentSectionId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
