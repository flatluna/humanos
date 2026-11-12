using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyMaster",
                schema: "dbo",
                columns: table => new
                {
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FamilySlug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HouseholdType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Bicultural"),
                    AdminFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdminEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AdminPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdminAzureOid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CoAdminFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CoAdminEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PrimaryStreet = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrimaryCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrimaryState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrimaryZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PrimaryCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "USA"),
                    SecondaryLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "America/Chicago"),
                    PreferredCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    PrimaryLanguage = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "es"),
                    IsBilingualHousehold = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AnnualEducationBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DigitalSunsetTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    FamilyMissionStatement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubscriptionTier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "FamilyEnterprise"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMaster", x => x.FamilyId);
                    table.ForeignKey(
                        name: "FK_FamilyMaster_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_FamilyMaster_FamilySlug",
                schema: "dbo",
                table: "FamilyMaster",
                column: "FamilySlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FamilyMaster_TenantId",
                schema: "dbo",
                table: "FamilyMaster",
                column: "TenantId",
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyMaster",
                schema: "dbo");
        }
    }
}
