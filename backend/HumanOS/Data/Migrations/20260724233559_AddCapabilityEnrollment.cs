using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapabilityEnrollment",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrolledDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityEnrollment", x => x.CapabilityEnrollmentId);
                    table.ForeignKey(
                        name: "FK_CapabilityEnrollment_Capability_CapabilityId",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapabilityEnrollment_Person_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityEnrollment_CapabilityId",
                schema: "dbo",
                table: "CapabilityEnrollment",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "UX_CapabilityEnrollment_Person_Capability",
                schema: "dbo",
                table: "CapabilityEnrollment",
                columns: new[] { "PersonId", "CapabilityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityEnrollment",
                schema: "dbo");
        }
    }
}
