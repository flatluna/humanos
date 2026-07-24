using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Program",
                schema: "dbo",
                columns: table => new
                {
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Objectives = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Requirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Program", x => x.ProgramId);
                });

            migrationBuilder.CreateTable(
                name: "ProgramCapability",
                schema: "dbo",
                columns: table => new
                {
                    ProgramCapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PhaseLabel = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramCapability", x => x.ProgramCapabilityId);
                    table.ForeignKey(
                        name: "FK_ProgramCapability_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramCapability_Program",
                        column: x => x.ProgramId,
                        principalSchema: "dbo",
                        principalTable: "Program",
                        principalColumn: "ProgramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramTranslation",
                schema: "dbo",
                columns: table => new
                {
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Objectives = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Requirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramTranslation", x => new { x.ProgramId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_ProgramTranslation_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramTranslation_Program",
                        column: x => x.ProgramId,
                        principalSchema: "dbo",
                        principalTable: "Program",
                        principalColumn: "ProgramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Program_Code",
                schema: "dbo",
                table: "Program",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCapability_CapabilityId",
                schema: "dbo",
                table: "ProgramCapability",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "UX_ProgramCapability_Program_Capability",
                schema: "dbo",
                table: "ProgramCapability",
                columns: new[] { "ProgramId", "CapabilityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProgramCapability_Program_SortOrder",
                schema: "dbo",
                table: "ProgramCapability",
                columns: new[] { "ProgramId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramTranslation_LanguageCode",
                schema: "dbo",
                table: "ProgramTranslation",
                column: "LanguageCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramCapability",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProgramTranslation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Program",
                schema: "dbo");
        }
    }
}
