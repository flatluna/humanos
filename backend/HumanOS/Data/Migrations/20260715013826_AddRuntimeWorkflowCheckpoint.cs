using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRuntimeWorkflowCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuntimeWorkflowCheckpoint",
                schema: "dbo",
                columns: table => new
                {
                    RuntimeWorkflowCheckpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CheckpointId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParentCheckpointId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeWorkflowCheckpoint", x => x.RuntimeWorkflowCheckpointId);
                });

            migrationBuilder.CreateIndex(
                name: "UX_RuntimeWorkflowCheckpoint_SessionId_CheckpointId",
                schema: "dbo",
                table: "RuntimeWorkflowCheckpoint",
                columns: new[] { "SessionId", "CheckpointId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuntimeWorkflowCheckpoint",
                schema: "dbo");
        }
    }
}
