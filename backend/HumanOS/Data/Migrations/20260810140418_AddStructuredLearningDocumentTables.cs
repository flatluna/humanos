using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningDocumentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StructuredLearningDocument",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningDocument", x => x.StructuredLearningDocumentId);
                });

            migrationBuilder.CreateTable(
                name: "StructuredLearningDocumentSection",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ChapterTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SubchapterTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalCharacters = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    Embedding = table.Column<SqlVector<float>>(type: "vector(1536)", nullable: false),
                    PageFrom = table.Column<int>(type: "int", nullable: true),
                    PageTo = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningDocumentSection", x => x.StructuredLearningDocumentSectionId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningDocumentSection_Document",
                        column: x => x.StructuredLearningDocumentId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocument",
                        principalColumn: "StructuredLearningDocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StructuredLearningEntity",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PrimarySectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningEntity", x => x.StructuredLearningEntityId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningEntity_Document",
                        column: x => x.StructuredLearningDocumentId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocument",
                        principalColumn: "StructuredLearningDocumentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StructuredLearningEntity_PrimarySection",
                        column: x => x.PrimarySectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StructuredLearningConnection",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StructuredLearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningConnection", x => x.StructuredLearningConnectionId);
                    table.ForeignKey(
                        name: "FK_StructuredLearningConnection_Document",
                        column: x => x.StructuredLearningDocumentId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocument",
                        principalColumn: "StructuredLearningDocumentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StructuredLearningConnection_SourceEntity",
                        column: x => x.SourceEntityId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningEntity",
                        principalColumn: "StructuredLearningEntityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StructuredLearningConnection_SourceSection",
                        column: x => x.SourceSectionId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningDocumentSection",
                        principalColumn: "StructuredLearningDocumentSectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StructuredLearningConnection_TargetEntity",
                        column: x => x.TargetEntityId,
                        principalSchema: "dbo",
                        principalTable: "StructuredLearningEntity",
                        principalColumn: "StructuredLearningEntityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningConnection_DocumentId",
                schema: "dbo",
                table: "StructuredLearningConnection",
                column: "StructuredLearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningConnection_SourceEntityId",
                schema: "dbo",
                table: "StructuredLearningConnection",
                column: "SourceEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningConnection_SourceSectionId",
                schema: "dbo",
                table: "StructuredLearningConnection",
                column: "SourceSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningConnection_TargetEntityId",
                schema: "dbo",
                table: "StructuredLearningConnection",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningConnection_TenantId",
                schema: "dbo",
                table: "StructuredLearningConnection",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningDocument_SessionId",
                schema: "dbo",
                table: "StructuredLearningDocument",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningDocument_TenantId",
                schema: "dbo",
                table: "StructuredLearningDocument",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningDocumentSection_DocumentId",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                column: "StructuredLearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningDocumentSection_TenantId",
                schema: "dbo",
                table: "StructuredLearningDocumentSection",
                column: "TenantId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningConnection",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StructuredLearningEntity",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StructuredLearningDocumentSection",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StructuredLearningDocument",
                schema: "dbo");
        }
    }
}
