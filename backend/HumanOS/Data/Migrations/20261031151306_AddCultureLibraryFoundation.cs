using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCultureLibraryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CulturePrinciple",
                schema: "dbo",
                columns: table => new
                {
                    CulturePrincipleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceCulturePrincipleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrincipleCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEs = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DescriptionEs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OwnerPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedByPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CulturePrinciple", x => x.CulturePrincipleId);
                    table.CheckConstraint("CK_CulturePrinciple_ApprovedState", "([Status] <> 'Approved') OR ([OwnerPersonId] IS NOT NULL AND [ApprovedByPersonId] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [EffectiveFrom] IS NOT NULL)");
                    table.CheckConstraint("CK_CulturePrinciple_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_CulturePrinciple_PrincipleCode_NotEmpty", "LEN(TRIM([PrincipleCode])) > 0");
                    table.CheckConstraint("CK_CulturePrinciple_SourceNotSelf", "[SourceCulturePrincipleId] IS NULL OR [SourceCulturePrincipleId] <> [CulturePrincipleId]");
                    table.CheckConstraint("CK_CulturePrinciple_Status", "[Status] IN ('Draft', 'Approved', 'Superseded', 'Retired')");
                    table.CheckConstraint("CK_CulturePrinciple_VersionNumber_Positive", "[VersionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_CulturePrinciple_ApprovedByPerson",
                        column: x => x.ApprovedByPersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CulturePrinciple_CreatedByPerson",
                        column: x => x.CreatedByPersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CulturePrinciple_OwnerPerson",
                        column: x => x.OwnerPersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CulturePrinciple_SourceCulturePrinciple",
                        column: x => x.SourceCulturePrincipleId,
                        principalSchema: "dbo",
                        principalTable: "CulturePrinciple",
                        principalColumn: "CulturePrincipleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CulturePrinciple_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CulturePrincipleBehavior",
                schema: "dbo",
                columns: table => new
                {
                    CulturePrincipleBehaviorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CulturePrincipleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BehaviorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BehaviorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DescriptionEs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CulturePrincipleBehavior", x => x.CulturePrincipleBehaviorId);
                    table.CheckConstraint("CK_CulturePrincipleBehavior_BehaviorCode_NotEmpty", "LEN(TRIM([BehaviorCode])) > 0");
                    table.CheckConstraint("CK_CulturePrincipleBehavior_BehaviorType", "[BehaviorType] IN ('ExpectedBehavior', 'BehaviorToAvoid', 'LeadershipExpectation', 'EmployeeExpectation', 'AITransformationImplication')");
                    table.CheckConstraint("CK_CulturePrincipleBehavior_DescriptionEn_NotEmpty", "LEN(TRIM([DescriptionEn])) > 0");
                    table.CheckConstraint("CK_CulturePrincipleBehavior_SortOrder_NonNegative", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_CulturePrincipleBehavior_CreatedByPerson",
                        column: x => x.CreatedByPersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CulturePrincipleBehavior_CulturePrinciple",
                        column: x => x.CulturePrincipleId,
                        principalSchema: "dbo",
                        principalTable: "CulturePrinciple",
                        principalColumn: "CulturePrincipleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CulturePrinciple_ApprovedByPersonId",
                schema: "dbo",
                table: "CulturePrinciple",
                column: "ApprovedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_CulturePrinciple_CreatedByPersonId",
                schema: "dbo",
                table: "CulturePrinciple",
                column: "CreatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_CulturePrinciple_OwnerPersonId",
                schema: "dbo",
                table: "CulturePrinciple",
                column: "OwnerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_CulturePrinciple_SourceCulturePrincipleId",
                schema: "dbo",
                table: "CulturePrinciple",
                column: "SourceCulturePrincipleId");

            migrationBuilder.CreateIndex(
                name: "IX_CulturePrinciple_TenantId",
                schema: "dbo",
                table: "CulturePrinciple",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_CulturePrinciple_Global_PrincipleCode_Version",
                schema: "dbo",
                table: "CulturePrinciple",
                columns: new[] { "PrincipleCode", "VersionNumber" },
                unique: true,
                filter: "[TenantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_CulturePrinciple_Tenant_PrincipleCode_Version",
                schema: "dbo",
                table: "CulturePrinciple",
                columns: new[] { "TenantId", "PrincipleCode", "VersionNumber" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CulturePrincipleBehavior_CreatedByPersonId",
                schema: "dbo",
                table: "CulturePrincipleBehavior",
                column: "CreatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_CulturePrincipleBehavior_Principle_Type_Active_Sort",
                schema: "dbo",
                table: "CulturePrincipleBehavior",
                columns: new[] { "CulturePrincipleId", "BehaviorType", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_CulturePrincipleBehavior_CulturePrincipleId_BehaviorCode",
                schema: "dbo",
                table: "CulturePrincipleBehavior",
                columns: new[] { "CulturePrincipleId", "BehaviorCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CulturePrincipleBehavior",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CulturePrinciple",
                schema: "dbo");
        }
    }
}
