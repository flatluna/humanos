using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGamificationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BadgeCatalog",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IconEmoji = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RuleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RuleThreshold = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerBadge",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BadgeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerBadge", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfile",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CurrentLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CurrentLevelName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrentStreakDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LongestStreakDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StreakFreezesAvailable = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LastActivityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalEngramsMastered = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RewardLedgerEntry",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EngramId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PointsBase = table.Column<int>(type: "int", nullable: false),
                    DifficultyMultiplier = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MemoryEffortMultiplier = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SurpriseMultiplier = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PointsAwarded = table.Column<int>(type: "int", nullable: false),
                    WasSurprise = table.Column<bool>(type: "bit", nullable: false),
                    SurpriseType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ImpactScoreAtTime = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EarnedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardLedgerEntry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_BadgeCatalog_Code",
                schema: "dbo",
                table: "BadgeCatalog",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBadge_PersonId",
                schema: "dbo",
                table: "PlayerBadge",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerBadge_Person_BadgeCode",
                schema: "dbo",
                table: "PlayerBadge",
                columns: new[] { "PersonId", "BadgeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PlayerProfile_PersonId",
                schema: "dbo",
                table: "PlayerProfile",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardLedgerEntry_PersonId",
                schema: "dbo",
                table: "RewardLedgerEntry",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UX_RewardLedgerEntry_Person_Course_Engram",
                schema: "dbo",
                table: "RewardLedgerEntry",
                columns: new[] { "PersonId", "CourseId", "EngramId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BadgeCatalog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PlayerBadge",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PlayerProfile",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RewardLedgerEntry",
                schema: "dbo");
        }
    }
}
