using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningTopicCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TopicId",
                schema: "dbo",
                table: "StructuredLearningEntity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TopicId",
                schema: "dbo",
                table: "StructuredLearningDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StructuredLearningTopic",
                schema: "dbo",
                columns: table => new
                {
                    StructuredLearningTopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredLearningTopic", x => x.StructuredLearningTopicId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructuredLearningTopic_TenantId_SubjectId",
                schema: "dbo",
                table: "StructuredLearningTopic",
                columns: new[] { "TenantId", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructuredLearningTopic",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "TopicId",
                schema: "dbo",
                table: "StructuredLearningEntity");

            migrationBuilder.DropColumn(
                name: "TopicId",
                schema: "dbo",
                table: "StructuredLearningDocument");
        }
    }
}
