using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "CapabilityDomain",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityDomainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityDomain", x => x.CapabilityDomainId);
                });

            migrationBuilder.CreateTable(
                name: "Goal",
                schema: "dbo",
                columns: table => new
                {
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goal", x => x.GoalId);
                    table.CheckConstraint("CK_Goal_Category", "[Category] IS NULL\r\nOR [Category] IN\r\n(\r\n    'PERSONAL_GROWTH',\r\n    'CAPABILITY_DEVELOPMENT',\r\n    'PROFESSIONAL',\r\n    'VALUE_CREATION',\r\n    'CONTRIBUTION',\r\n    'LIFE'\r\n)");
                });

            migrationBuilder.CreateTable(
                name: "Language",
                schema: "dbo",
                columns: table => new
                {
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NativeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Language", x => x.LanguageCode);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeSessionStatus",
                schema: "dbo",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false),
                    FinalStage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeSessionStatus", x => x.SessionId);
                });

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

            migrationBuilder.CreateTable(
                name: "Subject",
                schema: "dbo",
                columns: table => new
                {
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subject", x => x.SubjectId);
                });

            migrationBuilder.CreateTable(
                name: "Tenant",
                schema: "dbo",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CultureCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "en-US"),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "UTC"),
                    AzureTenantId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenant", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityDomainTranslation",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityDomainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityDomainTranslation", x => new { x.CapabilityDomainId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_CapabilityDomainTranslation_Domain",
                        column: x => x.CapabilityDomainId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityDomain",
                        principalColumn: "CapabilityDomainId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapabilityDomainTranslation_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalTranslation",
                schema: "dbo",
                columns: table => new
                {
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalTranslation", x => new { x.GoalId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_GoalTranslation_Goal",
                        column: x => x.GoalId,
                        principalSchema: "dbo",
                        principalTable: "Goal",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalTranslation_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Capability",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityDomainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Capability", x => x.CapabilityId);
                    table.ForeignKey(
                        name: "FK_Capability_CapabilityDomain",
                        column: x => x.CapabilityDomainId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityDomain",
                        principalColumn: "CapabilityDomainId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Capability_Subject",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SubjectTranslation",
                schema: "dbo",
                columns: table => new
                {
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectTranslation", x => new { x.SubjectId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_SubjectTranslation_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectTranslation_Subject",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Person",
                schema: "dbo",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AzureOid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AzureTid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Person", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_Person_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Agent",
                schema: "dbo",
                columns: table => new
                {
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agent", x => x.AgentId);
                    table.ForeignKey(
                        name: "FK_Agent_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assessment",
                schema: "dbo",
                columns: table => new
                {
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AssessmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PassingScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 70m),
                    MaxScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 100m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessment", x => x.AssessmentId);
                    table.CheckConstraint("CK_Assessment_Scores", "[MaxScore] > 0\r\nAND [PassingScore] BETWEEN 0 AND [MaxScore]");
                    table.ForeignKey(
                        name: "FK_Assessment_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityGraphs",
                columns: table => new
                {
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KeyEntitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverImageStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphs", x => x.CapabilityGraphId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphs_Capability_CapabilityId",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityLevel",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Layer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    HumanTransformation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityLevel", x => x.CapabilityLevelId);
                    table.ForeignKey(
                        name: "FK_CapabilityLevel_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityTranslation",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityTranslation", x => new { x.CapabilityId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_CapabilityTranslation_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapabilityTranslation_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalCapability",
                schema: "dbo",
                columns: table => new
                {
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalCapability", x => new { x.GoalId, x.CapabilityId });
                    table.ForeignKey(
                        name: "FK_GoalCapability_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalCapability_Goal",
                        column: x => x.GoalId,
                        principalSchema: "dbo",
                        principalTable: "Goal",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Project",
                schema: "dbo",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DifficultyLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    EstimatedHours = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project", x => x.ProjectId);
                    table.CheckConstraint("CK_Project_DifficultyLevel", "[DifficultyLevel] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Project_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HumanProfile",
                schema: "dbo",
                columns: table => new
                {
                    HumanProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MissionStatement = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PrimaryGoal = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LearningStyle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CurrentLifeStage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WeeklyAvailabilityHours = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    MotivationScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanProfile", x => x.HumanProfileId);
                    table.CheckConstraint("CK_HumanProfile_Scores", "([MotivationScore] IS NULL\r\n    OR [MotivationScore] BETWEEN 0 AND 100)\r\nAND\r\n([ConfidenceScore] IS NULL\r\n    OR [ConfidenceScore] BETWEEN 0 AND 100)");
                    table.CheckConstraint("CK_HumanProfile_WeeklyAvailabilityHours", "[WeeklyAvailabilityHours] IS NULL\r\nOR [WeeklyAvailabilityHours] BETWEEN 0 AND 168");
                    table.ForeignKey(
                        name: "FK_HumanProfile_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HumanState",
                schema: "dbo",
                columns: table => new
                {
                    HumanStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Energy = table.Column<int>(type: "int", nullable: true),
                    Focus = table.Column<int>(type: "int", nullable: true),
                    Streak = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanState", x => x.HumanStateId);
                    table.ForeignKey(
                        name: "FK_HumanState_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HumanState_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobDescription",
                schema: "dbo",
                columns: table => new
                {
                    JobDescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SourceUploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RolePurpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RoleSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PrimaryResponsibilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    ExpectedOutcomesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    RequiredExperience = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ToolsMentionedJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    SuggestedProfessionalLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtractionStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ExtractionModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RawExtractionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtractedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDescription", x => x.JobDescriptionId);
                    table.ForeignKey(
                        name: "FK_JobDescription_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningSessions",
                columns: table => new
                {
                    LearningSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessions", x => x.LearningSessionId);
                    table.ForeignKey(
                        name: "FK_LearningSessions_Capability_CapabilityId",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningSessions_Person_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonCapability",
                schema: "dbo",
                columns: table => new
                {
                    PersonCapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TargetLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    ProgressPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    MasteryScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "NotStarted"),
                    IndependenceLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RetentionScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    KnowledgeScore = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RecallScore = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ApplicationScore = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActivityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonCapability", x => x.PersonCapabilityId);
                    table.CheckConstraint("CK_PersonCapability_ConfidenceScore", "[ConfidenceScore] IS NULL\r\nOR [ConfidenceScore] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PersonCapability_CurrentLevel", "[CurrentLevel] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_PersonCapability_IndependenceLevel", "[IndependenceLevel] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_PersonCapability_MasteryScore", "[MasteryScore] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PersonCapability_ProgressPercentage", "[ProgressPercentage] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PersonCapability_RetentionScore", "[RetentionScore] IS NULL\r\nOR [RetentionScore] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PersonCapability_Status", "[Status] IN\r\n('NotStarted', 'InProgress', 'Paused', 'Completed')");
                    table.CheckConstraint("CK_PersonCapability_TargetLevel", "[TargetLevel] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_PersonCapability_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonCapability_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonGoal",
                schema: "dbo",
                columns: table => new
                {
                    PersonGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Active"),
                    ProgressPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    TargetDate = table.Column<DateTime>(type: "date", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonGoal", x => x.PersonGoalId);
                    table.CheckConstraint("CK_PersonGoal_ProgressPercentage", "[ProgressPercentage] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PersonGoal_Status", "[Status] IN\r\n(\r\n    'Active',\r\n    'Paused',\r\n    'Completed',\r\n    'Abandoned'\r\n)");
                    table.ForeignKey(
                        name: "FK_PersonGoal_Goal",
                        column: x => x.GoalId,
                        principalSchema: "dbo",
                        principalTable: "Goal",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonGoal_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentMessage",
                schema: "dbo",
                columns: table => new
                {
                    AgentMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMessage", x => x.AgentMessageId);
                    table.ForeignKey(
                        name: "FK_AgentMessage_Agent",
                        column: x => x.AgentId,
                        principalSchema: "dbo",
                        principalTable: "Agent",
                        principalColumn: "AgentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentMessage_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentMessage_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentAttempt",
                schema: "dbo",
                columns: table => new
                {
                    AssessmentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AssistanceLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentAttempt", x => x.AssessmentAttemptId);
                    table.CheckConstraint("CK_AssessmentAttempt_AssistanceLevel", "[AssistanceLevel] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_AssessmentAttempt_Score", "[Score] IS NULL\r\nOR [Score] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_AssessmentAttempt_Assessment",
                        column: x => x.AssessmentId,
                        principalSchema: "dbo",
                        principalTable: "Assessment",
                        principalColumn: "AssessmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentAttempt_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityGraphNodes",
                columns: table => new
                {
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NodeType = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AcademicDefinition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Interpretation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExamplesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplicationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphNodes", x => x.CapabilityGraphNodeId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodes_CapabilityGraphs_CapabilityGraphId",
                        column: x => x.CapabilityGraphId,
                        principalTable: "CapabilityGraphs",
                        principalColumn: "CapabilityGraphId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityModule",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Script = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReflectionPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetricRationale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecallRequirement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LearnerProduction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LearnerTask = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModule", x => x.CapabilityModuleId);
                    table.ForeignKey(
                        name: "FK_CapabilityModule_CapabilityLevel",
                        column: x => x.CapabilityLevelId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityLevel",
                        principalColumn: "CapabilityLevelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonProject",
                schema: "dbo",
                columns: table => new
                {
                    PersonProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "NotStarted"),
                    ProgressPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonProject", x => x.PersonProjectId);
                    table.CheckConstraint("CK_PersonProject_ProgressPercentage", "[ProgressPercentage] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PersonProject_Status", "[Status] IN\r\n(\r\n    'NotStarted',\r\n    'InProgress',\r\n    'Paused',\r\n    'Completed'\r\n)");
                    table.ForeignKey(
                        name: "FK_PersonProject_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonProject_Project",
                        column: x => x.ProjectId,
                        principalSchema: "dbo",
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTranslation",
                schema: "dbo",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTranslation", x => new { x.ProjectId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_ProjectTranslation_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectTranslation_Project",
                        column: x => x.ProjectId,
                        principalSchema: "dbo",
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonProfile",
                schema: "dbo",
                columns: table => new
                {
                    PersonProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    CountryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProfilePhotoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Company = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Biography = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CurrentJobDescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonProfile", x => x.PersonProfileId);
                    table.ForeignKey(
                        name: "FK_PersonProfile_CurrentJobDescription",
                        column: x => x.CurrentJobDescriptionId,
                        principalSchema: "dbo",
                        principalTable: "JobDescription",
                        principalColumn: "JobDescriptionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PersonProfile_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonProfile_PreferredLanguage",
                        column: x => x.PreferredLanguage,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityPractice",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityPracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonCapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PracticeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssistanceLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PersonReflection = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    PracticedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityPractice", x => x.CapabilityPracticeId);
                    table.CheckConstraint("CK_CapabilityPractice_AssistanceLevel", "[AssistanceLevel] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_CapabilityPractice_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapabilityPractice_PersonCapability",
                        column: x => x.PersonCapabilityId,
                        principalSchema: "dbo",
                        principalTable: "PersonCapability",
                        principalColumn: "PersonCapabilityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecallAttempt",
                schema: "dbo",
                columns: table => new
                {
                    RecallAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonCapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecallPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PersonResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecallScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AssistanceLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    AttemptedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallAttempt", x => x.RecallAttemptId);
                    table.CheckConstraint("CK_RecallAttempt_AssistanceLevel", "[AssistanceLevel] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_RecallAttempt_ConfidenceScore", "[ConfidenceScore] IS NULL\r\nOR [ConfidenceScore] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_RecallAttempt_RecallScore", "[RecallScore] IS NULL\r\nOR [RecallScore] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_RecallAttempt_Language",
                        column: x => x.LanguageCode,
                        principalSchema: "dbo",
                        principalTable: "Language",
                        principalColumn: "LanguageCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecallAttempt_PersonCapability",
                        column: x => x.PersonCapabilityId,
                        principalSchema: "dbo",
                        principalTable: "PersonCapability",
                        principalColumn: "PersonCapabilityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityGraphEdges",
                columns: table => new
                {
                    CapabilityGraphEdgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphEdges", x => x.CapabilityGraphEdgeId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphEdges_CapabilityGraphNodes_SourceNodeId",
                        column: x => x.SourceNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId");
                    table.ForeignKey(
                        name: "FK_CapabilityGraphEdges_CapabilityGraphNodes_TargetNodeId",
                        column: x => x.TargetNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId");
                    table.ForeignKey(
                        name: "FK_CapabilityGraphEdges_CapabilityGraphs_CapabilityGraphId",
                        column: x => x.CapabilityGraphId,
                        principalTable: "CapabilityGraphs",
                        principalColumn: "CapabilityGraphId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityGraphNodeIllustrations",
                columns: table => new
                {
                    CapabilityGraphNodeIllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    ImageModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphNodeIllustrations", x => x.CapabilityGraphNodeIllustrationId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodeIllustrations_CapabilityGraphNodes_CapabilityGraphNodeId",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityGraphNodeKnowledgeChunk",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityGraphNodeKnowledgeChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceField = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<SqlVector<float>>(type: "vector(1536)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphNodeKnowledgeChunk", x => x.CapabilityGraphNodeKnowledgeChunkId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphNode",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NodeExperienceBlueprints",
                columns: table => new
                {
                    NodeExperienceBlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeExperienceBlueprints", x => x.NodeExperienceBlueprintId);
                    table.ForeignKey(
                        name: "FK_NodeExperienceBlueprints_CapabilityGraphNodes_CapabilityGraphNodeId",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityKnowledgeChunk",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityKnowledgeChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<SqlVector<float>>(type: "vector(1536)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityKnowledgeChunk", x => x.CapabilityKnowledgeChunkId);
                    table.ForeignKey(
                        name: "FK_CapabilityKnowledgeChunk_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapabilityKnowledgeChunk_CapabilityModule",
                        column: x => x.CapabilityModuleId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModule",
                        principalColumn: "CapabilityModuleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityModuleChapter",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleChapterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TeachingContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPrimaryWeight = table.Column<bool>(type: "bit", nullable: false),
                    RecallPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCumulativeRecall = table.Column<bool>(type: "bit", nullable: false),
                    PredictionPrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MiniPracticePrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModuleChapter", x => x.CapabilityModuleChapterId);
                    table.ForeignKey(
                        name: "FK_CapabilityModuleChapter_CapabilityModule",
                        column: x => x.CapabilityModuleId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModule",
                        principalColumn: "CapabilityModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityModuleMetric",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModuleMetric", x => new { x.CapabilityModuleId, x.Metric });
                    table.ForeignKey(
                        name: "FK_CapabilityModuleMetric_CapabilityModule",
                        column: x => x.CapabilityModuleId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModule",
                        principalColumn: "CapabilityModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityModuleVerification",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleVerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetMetric = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecallStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecallEvidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecallEvidenceLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecallOccursBeforeInstruction = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModuleVerification", x => x.CapabilityModuleVerificationId);
                    table.ForeignKey(
                        name: "FK_CapabilityModuleVerification_CapabilityModule",
                        column: x => x.CapabilityModuleId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModule",
                        principalColumn: "CapabilityModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evidence",
                schema: "dbo",
                columns: table => new
                {
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EvidenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValidationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    AssistanceLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ValidationFeedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidence", x => x.EvidenceId);
                    table.CheckConstraint("CK_Evidence_AssistanceLevel", "[AssistanceLevel] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Evidence_ValidationState", "(\r\n    [ValidationStatus] = 'Pending'\r\n    AND [ValidatedDate] IS NULL\r\n)\r\nOR\r\n(\r\n    [ValidationStatus] IN ('Accepted', 'Rejected')\r\n    AND [ValidatedDate] IS NOT NULL\r\n)");
                    table.CheckConstraint("CK_Evidence_ValidationStatus", "[ValidationStatus] IN\r\n(\r\n    'Pending',\r\n    'Accepted',\r\n    'Rejected'\r\n)");
                    table.ForeignKey(
                        name: "FK_Evidence_Capability",
                        column: x => x.CapabilityId,
                        principalSchema: "dbo",
                        principalTable: "Capability",
                        principalColumn: "CapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evidence_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evidence_PersonProject",
                        column: x => x.PersonProjectId,
                        principalSchema: "dbo",
                        principalTable: "PersonProject",
                        principalColumn: "PersonProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GrowthAction",
                schema: "dbo",
                columns: table => new
                {
                    GrowthActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonCapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ScheduledFor = table.Column<DateOnly>(type: "date", nullable: true),
                    RecallAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrowthAction", x => x.GrowthActionId);
                    table.ForeignKey(
                        name: "FK_GrowthAction_Assessment",
                        column: x => x.AssessmentId,
                        principalSchema: "dbo",
                        principalTable: "Assessment",
                        principalColumn: "AssessmentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GrowthAction_CapabilityPractice",
                        column: x => x.PracticeId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityPractice",
                        principalColumn: "CapabilityPracticeId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GrowthAction_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrowthAction_PersonCapability",
                        column: x => x.PersonCapabilityId,
                        principalSchema: "dbo",
                        principalTable: "PersonCapability",
                        principalColumn: "PersonCapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrowthAction_RecallAttempt",
                        column: x => x.RecallAttemptId,
                        principalSchema: "dbo",
                        principalTable: "RecallAttempt",
                        principalColumn: "RecallAttemptId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GrowthAction_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityGraphNodeKnowledgeExpansions",
                columns: table => new
                {
                    CapabilityGraphNodeKnowledgeExpansionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiagramIllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityGraphNodeKnowledgeExpansions", x => x.CapabilityGraphNodeKnowledgeExpansionId);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodeKnowledgeExpansions_CapabilityGraphNodeIllustrations_DiagramIllustrationId",
                        column: x => x.DiagramIllustrationId,
                        principalTable: "CapabilityGraphNodeIllustrations",
                        principalColumn: "CapabilityGraphNodeIllustrationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapabilityGraphNodeKnowledgeExpansions_CapabilityGraphNodes_CapabilityGraphNodeId",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintValidations",
                columns: table => new
                {
                    BlueprintValidationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeExperienceBlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintValidations", x => x.BlueprintValidationId);
                    table.ForeignKey(
                        name: "FK_BlueprintValidations_NodeExperienceBlueprints_NodeExperienceBlueprintId",
                        column: x => x.NodeExperienceBlueprintId,
                        principalTable: "NodeExperienceBlueprints",
                        principalColumn: "NodeExperienceBlueprintId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningSessionNodes",
                columns: table => new
                {
                    LearningSessionNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapabilityGraphNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeExperienceBlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessionNodes", x => x.LearningSessionNodeId);
                    table.ForeignKey(
                        name: "FK_LearningSessionNodes_CapabilityGraphNodes_CapabilityGraphNodeId",
                        column: x => x.CapabilityGraphNodeId,
                        principalTable: "CapabilityGraphNodes",
                        principalColumn: "CapabilityGraphNodeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningSessionNodes_LearningSessions_LearningSessionId",
                        column: x => x.LearningSessionId,
                        principalTable: "LearningSessions",
                        principalColumn: "LearningSessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningSessionNodes_NodeExperienceBlueprints_NodeExperienceBlueprintId",
                        column: x => x.NodeExperienceBlueprintId,
                        principalTable: "NodeExperienceBlueprints",
                        principalColumn: "NodeExperienceBlueprintId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NodeExperienceBlueprintSteps",
                columns: table => new
                {
                    NodeExperienceBlueprintStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NodeExperienceBlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepType = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferencedIllustrationIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeExperienceBlueprintSteps", x => x.NodeExperienceBlueprintStepId);
                    table.ForeignKey(
                        name: "FK_NodeExperienceBlueprintSteps_NodeExperienceBlueprints_NodeExperienceBlueprintId",
                        column: x => x.NodeExperienceBlueprintId,
                        principalTable: "NodeExperienceBlueprints",
                        principalColumn: "NodeExperienceBlueprintId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityModuleSuccessCriterionResult",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityModuleSuccessCriterionResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CapabilityModuleVerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Criterion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSatisfied = table.Column<bool>(type: "bit", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityModuleSuccessCriterionResult", x => x.CapabilityModuleSuccessCriterionResultId);
                    table.ForeignKey(
                        name: "FK_CapabilityModuleSuccessCriterionResult_CapabilityModuleVerification",
                        column: x => x.CapabilityModuleVerificationId,
                        principalSchema: "dbo",
                        principalTable: "CapabilityModuleVerification",
                        principalColumn: "CapabilityModuleVerificationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityEvidence",
                schema: "dbo",
                columns: table => new
                {
                    CapabilityEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonCapabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContributionWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ValidationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    ValidatedByPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    EvidenceId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PersonCapabilityId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityEvidence", x => x.CapabilityEvidenceId);
                    table.ForeignKey(
                        name: "FK_CapabilityEvidence_Evidence",
                        column: x => x.EvidenceId,
                        principalSchema: "dbo",
                        principalTable: "Evidence",
                        principalColumn: "EvidenceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapabilityEvidence_Evidence_EvidenceId1",
                        column: x => x.EvidenceId1,
                        principalSchema: "dbo",
                        principalTable: "Evidence",
                        principalColumn: "EvidenceId");
                    table.ForeignKey(
                        name: "FK_CapabilityEvidence_PersonCapability",
                        column: x => x.PersonCapabilityId,
                        principalSchema: "dbo",
                        principalTable: "PersonCapability",
                        principalColumn: "PersonCapabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapabilityEvidence_PersonCapability_PersonCapabilityId1",
                        column: x => x.PersonCapabilityId1,
                        principalSchema: "dbo",
                        principalTable: "PersonCapability",
                        principalColumn: "PersonCapabilityId");
                    table.ForeignKey(
                        name: "FK_CapabilityEvidence_Validator",
                        column: x => x.ValidatedByPersonId,
                        principalSchema: "dbo",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintValidationIssues",
                columns: table => new
                {
                    BlueprintValidationIssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlueprintValidationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintValidationIssues", x => x.BlueprintValidationIssueId);
                    table.ForeignKey(
                        name: "FK_BlueprintValidationIssues_BlueprintValidations_BlueprintValidationId",
                        column: x => x.BlueprintValidationId,
                        principalTable: "BlueprintValidations",
                        principalColumn: "BlueprintValidationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintValidationMetrics",
                columns: table => new
                {
                    BlueprintValidationMetricId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlueprintValidationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetricValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintValidationMetrics", x => x.BlueprintValidationMetricId);
                    table.ForeignKey(
                        name: "FK_BlueprintValidationMetrics_BlueprintValidations_BlueprintValidationId",
                        column: x => x.BlueprintValidationId,
                        principalTable: "BlueprintValidations",
                        principalColumn: "BlueprintValidationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentRounds",
                columns: table => new
                {
                    AssessmentRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FinalScore = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentRounds", x => x.AssessmentRoundId);
                    table.ForeignKey(
                        name: "FK_AssessmentRounds_LearningSessionNodes_LearningSessionNodeId",
                        column: x => x.LearningSessionNodeId,
                        principalTable: "LearningSessionNodes",
                        principalColumn: "LearningSessionNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningAssessmentResults",
                columns: table => new
                {
                    LearningAssessmentResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAssessmentResults", x => x.LearningAssessmentResultId);
                    table.ForeignKey(
                        name: "FK_LearningAssessmentResults_LearningSessionNodes_LearningSessionNodeId",
                        column: x => x.LearningSessionNodeId,
                        principalTable: "LearningSessionNodes",
                        principalColumn: "LearningSessionNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningSessionSteps",
                columns: table => new
                {
                    LearningSessionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CurrentRecallPrompt = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessionSteps", x => x.LearningSessionStepId);
                    table.ForeignKey(
                        name: "FK_LearningSessionSteps_LearningSessionNodes_LearningSessionNodeId",
                        column: x => x.LearningSessionNodeId,
                        principalTable: "LearningSessionNodes",
                        principalColumn: "LearningSessionNodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentQuestions",
                columns: table => new
                {
                    AssessmentQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionIndex = table.Column<int>(type: "int", nullable: false),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Correctness = table.Column<int>(type: "int", nullable: false),
                    ScoreContribution = table.Column<int>(type: "int", nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObservedError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IllustrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AnsweredDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentQuestions", x => x.AssessmentQuestionId);
                    table.ForeignKey(
                        name: "FK_AssessmentQuestions_AssessmentRounds_AssessmentRoundId",
                        column: x => x.AssessmentRoundId,
                        principalTable: "AssessmentRounds",
                        principalColumn: "AssessmentRoundId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssessmentQuestions_CapabilityGraphNodeIllustrations_IllustrationId",
                        column: x => x.IllustrationId,
                        principalTable: "CapabilityGraphNodeIllustrations",
                        principalColumn: "CapabilityGraphNodeIllustrationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningEvidences",
                columns: table => new
                {
                    LearningEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningSessionStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentResponse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TutorPrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TutorScore = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningEvidences", x => x.LearningEvidenceId);
                    table.ForeignKey(
                        name: "FK_LearningEvidences_LearningSessionSteps_LearningSessionStepId",
                        column: x => x.LearningSessionStepId,
                        principalTable: "LearningSessionSteps",
                        principalColumn: "LearningSessionStepId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Agent_CapabilityId",
                schema: "dbo",
                table: "Agent",
                column: "CapabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessage_AgentId",
                schema: "dbo",
                table: "AgentMessage",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessage_PersonId_CreatedDate",
                schema: "dbo",
                table: "AgentMessage",
                columns: new[] { "PersonId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessage_TenantId",
                schema: "dbo",
                table: "AgentMessage",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessment_CapabilityId",
                schema: "dbo",
                table: "Assessment",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempt_AssessmentId",
                schema: "dbo",
                table: "AssessmentAttempt",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempt_PersonId",
                schema: "dbo",
                table: "AssessmentAttempt",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentQuestions_AssessmentRoundId_QuestionIndex",
                table: "AssessmentQuestions",
                columns: new[] { "AssessmentRoundId", "QuestionIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentQuestions_IllustrationId",
                table: "AssessmentQuestions",
                column: "IllustrationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRounds_LearningSessionNodeId_RoundNumber",
                table: "AssessmentRounds",
                columns: new[] { "LearningSessionNodeId", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintValidationIssues_BlueprintValidationId",
                table: "BlueprintValidationIssues",
                column: "BlueprintValidationId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintValidationMetrics_BlueprintValidationId",
                table: "BlueprintValidationMetrics",
                column: "BlueprintValidationId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintValidations_NodeExperienceBlueprintId",
                table: "BlueprintValidations",
                column: "NodeExperienceBlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_Capability_CapabilityDomainId",
                schema: "dbo",
                table: "Capability",
                column: "CapabilityDomainId");

            migrationBuilder.CreateIndex(
                name: "IX_Capability_SubjectId",
                schema: "dbo",
                table: "Capability",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "UX_Capability_Code",
                schema: "dbo",
                table: "Capability",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CapabilityDomain_Code",
                schema: "dbo",
                table: "CapabilityDomain",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityDomainTranslation_LanguageCode",
                schema: "dbo",
                table: "CapabilityDomainTranslation",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityEvidence_EvidenceId",
                schema: "dbo",
                table: "CapabilityEvidence",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityEvidence_EvidenceId1",
                schema: "dbo",
                table: "CapabilityEvidence",
                column: "EvidenceId1");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityEvidence_PersonCapabilityId1",
                schema: "dbo",
                table: "CapabilityEvidence",
                column: "PersonCapabilityId1");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityEvidence_ValidatedByPersonId",
                schema: "dbo",
                table: "CapabilityEvidence",
                column: "ValidatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "UQ_CapabilityEvidence",
                schema: "dbo",
                table: "CapabilityEvidence",
                columns: new[] { "PersonCapabilityId", "EvidenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphEdges_CapabilityGraphId",
                table: "CapabilityGraphEdges",
                column: "CapabilityGraphId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphEdges_SourceNodeId_TargetNodeId",
                table: "CapabilityGraphEdges",
                columns: new[] { "SourceNodeId", "TargetNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphEdges_TargetNodeId",
                table: "CapabilityGraphEdges",
                column: "TargetNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeIllustrations_CapabilityGraphNodeId",
                table: "CapabilityGraphNodeIllustrations",
                column: "CapabilityGraphNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphId",
                schema: "dbo",
                table: "CapabilityGraphNodeKnowledgeChunk",
                column: "CapabilityGraphId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphNodeId",
                schema: "dbo",
                table: "CapabilityGraphNodeKnowledgeChunk",
                column: "CapabilityGraphNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeKnowledgeExpansions_CapabilityGraphNodeId",
                table: "CapabilityGraphNodeKnowledgeExpansions",
                column: "CapabilityGraphNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodeKnowledgeExpansions_DiagramIllustrationId",
                table: "CapabilityGraphNodeKnowledgeExpansions",
                column: "DiagramIllustrationId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodes_CapabilityGraphId_Name",
                table: "CapabilityGraphNodes",
                columns: new[] { "CapabilityGraphId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphNodes_CapabilityGraphId_SortOrder",
                table: "CapabilityGraphNodes",
                columns: new[] { "CapabilityGraphId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityGraphs_CapabilityId",
                table: "CapabilityGraphs",
                column: "CapabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityKnowledgeChunk_CapabilityId",
                schema: "dbo",
                table: "CapabilityKnowledgeChunk",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityKnowledgeChunk_CapabilityModuleId",
                schema: "dbo",
                table: "CapabilityKnowledgeChunk",
                column: "CapabilityModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityLevel_CapabilityId",
                schema: "dbo",
                table: "CapabilityLevel",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityModule_CapabilityLevelId",
                schema: "dbo",
                table: "CapabilityModule",
                column: "CapabilityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityModuleChapter_CapabilityModuleId",
                schema: "dbo",
                table: "CapabilityModuleChapter",
                column: "CapabilityModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityModuleSuccessCriterionResult_CapabilityModuleVerificationId",
                schema: "dbo",
                table: "CapabilityModuleSuccessCriterionResult",
                column: "CapabilityModuleVerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityModuleVerification_CapabilityModuleId",
                schema: "dbo",
                table: "CapabilityModuleVerification",
                column: "CapabilityModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityPractice_LanguageCode",
                schema: "dbo",
                table: "CapabilityPractice",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityPractice_PersonCapabilityId",
                schema: "dbo",
                table: "CapabilityPractice",
                column: "PersonCapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityTranslation_LanguageCode",
                schema: "dbo",
                table: "CapabilityTranslation",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_CapabilityId",
                schema: "dbo",
                table: "Evidence",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_PersonId",
                schema: "dbo",
                table: "Evidence",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_PersonProjectId",
                schema: "dbo",
                table: "Evidence",
                column: "PersonProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalCapability_CapabilityId",
                schema: "dbo",
                table: "GoalCapability",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalTranslation_LanguageCode",
                schema: "dbo",
                table: "GoalTranslation",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthAction_AssessmentId",
                schema: "dbo",
                table: "GrowthAction",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthAction_PersonCapabilityId",
                schema: "dbo",
                table: "GrowthAction",
                column: "PersonCapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthAction_PersonId_ScheduledFor",
                schema: "dbo",
                table: "GrowthAction",
                columns: new[] { "PersonId", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_GrowthAction_PracticeId",
                schema: "dbo",
                table: "GrowthAction",
                column: "PracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthAction_RecallAttemptId",
                schema: "dbo",
                table: "GrowthAction",
                column: "RecallAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthAction_TenantId",
                schema: "dbo",
                table: "GrowthAction",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_HumanProfile_PersonId",
                schema: "dbo",
                table: "HumanProfile",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HumanState_PersonId_RecordedAt",
                schema: "dbo",
                table: "HumanState",
                columns: new[] { "PersonId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HumanState_TenantId",
                schema: "dbo",
                table: "HumanState",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobDescription_PersonId",
                schema: "dbo",
                table: "JobDescription",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_JobDescription_TenantId",
                schema: "dbo",
                table: "JobDescription",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningAssessmentResults_LearningSessionNodeId",
                table: "LearningAssessmentResults",
                column: "LearningSessionNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningEvidences_LearningSessionStepId",
                table: "LearningEvidences",
                column: "LearningSessionStepId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionNodes_CapabilityGraphNodeId",
                table: "LearningSessionNodes",
                column: "CapabilityGraphNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionNodes_LearningSessionId",
                table: "LearningSessionNodes",
                column: "LearningSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionNodes_NodeExperienceBlueprintId",
                table: "LearningSessionNodes",
                column: "NodeExperienceBlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_CapabilityId",
                table: "LearningSessions",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_PersonId",
                table: "LearningSessions",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_PersonId_CapabilityId",
                table: "LearningSessions",
                columns: new[] { "PersonId", "CapabilityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionSteps_LearningSessionNodeId_StepType",
                table: "LearningSessionSteps",
                columns: new[] { "LearningSessionNodeId", "StepType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodeExperienceBlueprints_CapabilityGraphNodeId",
                table: "NodeExperienceBlueprints",
                column: "CapabilityGraphNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeExperienceBlueprints_CapabilityGraphNodeId_Name_Version",
                table: "NodeExperienceBlueprints",
                columns: new[] { "CapabilityGraphNodeId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodeExperienceBlueprintSteps_NodeExperienceBlueprintId_SortOrder",
                table: "NodeExperienceBlueprintSteps",
                columns: new[] { "NodeExperienceBlueprintId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeExperienceBlueprintSteps_NodeExperienceBlueprintId_StepType",
                table: "NodeExperienceBlueprintSteps",
                columns: new[] { "NodeExperienceBlueprintId", "StepType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_TenantId",
                schema: "dbo",
                table: "Person",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_Person_AzureTid_AzureOid",
                schema: "dbo",
                table: "Person",
                columns: new[] { "AzureTid", "AzureOid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonCapability_CapabilityId",
                schema: "dbo",
                table: "PersonCapability",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "UX_PersonCapability_PersonId_CapabilityId",
                schema: "dbo",
                table: "PersonCapability",
                columns: new[] { "PersonId", "CapabilityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonGoal_GoalId",
                schema: "dbo",
                table: "PersonGoal",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "UX_PersonGoal_PersonId_GoalId",
                schema: "dbo",
                table: "PersonGoal",
                columns: new[] { "PersonId", "GoalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonProfile_CurrentJobDescriptionId",
                schema: "dbo",
                table: "PersonProfile",
                column: "CurrentJobDescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonProfile_PreferredLanguage",
                schema: "dbo",
                table: "PersonProfile",
                column: "PreferredLanguage");

            migrationBuilder.CreateIndex(
                name: "UX_PersonProfile_PersonId",
                schema: "dbo",
                table: "PersonProfile",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonProject_ProjectId",
                schema: "dbo",
                table: "PersonProject",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "UX_PersonProject_PersonId_ProjectId",
                schema: "dbo",
                table: "PersonProject",
                columns: new[] { "PersonId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Project_CapabilityId",
                schema: "dbo",
                table: "Project",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTranslation_LanguageCode",
                schema: "dbo",
                table: "ProjectTranslation",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_RecallAttempt_LanguageCode",
                schema: "dbo",
                table: "RecallAttempt",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_RecallAttempt_PersonCapabilityId",
                schema: "dbo",
                table: "RecallAttempt",
                column: "PersonCapabilityId");

            migrationBuilder.CreateIndex(
                name: "UX_RuntimeWorkflowCheckpoint_SessionId_CheckpointId",
                schema: "dbo",
                table: "RuntimeWorkflowCheckpoint",
                columns: new[] { "SessionId", "CheckpointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Subject_Code",
                schema: "dbo",
                table: "Subject",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTranslation_LanguageCode",
                schema: "dbo",
                table: "SubjectTranslation",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "UX_Tenant_AzureTenantId",
                schema: "dbo",
                table: "Tenant",
                column: "AzureTenantId",
                unique: true,
                filter: "[AzureTenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Tenant_Slug",
                schema: "dbo",
                table: "Tenant",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentMessage",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AssessmentAttempt",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AssessmentQuestions");

            migrationBuilder.DropTable(
                name: "BlueprintValidationIssues");

            migrationBuilder.DropTable(
                name: "BlueprintValidationMetrics");

            migrationBuilder.DropTable(
                name: "CapabilityDomainTranslation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityEvidence",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityGraphEdges");

            migrationBuilder.DropTable(
                name: "CapabilityGraphNodeKnowledgeChunk",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityGraphNodeKnowledgeExpansions");

            migrationBuilder.DropTable(
                name: "CapabilityKnowledgeChunk",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityModuleChapter",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityModuleMetric",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityModuleSuccessCriterionResult",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityTranslation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GoalCapability",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GoalTranslation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GrowthAction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "HumanProfile",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "HumanState",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LearningAssessmentResults");

            migrationBuilder.DropTable(
                name: "LearningEvidences");

            migrationBuilder.DropTable(
                name: "NodeExperienceBlueprintSteps");

            migrationBuilder.DropTable(
                name: "PersonGoal",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PersonProfile",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProjectTranslation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RuntimeSessionStatus",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RuntimeWorkflowCheckpoint",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SubjectTranslation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Agent",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AssessmentRounds");

            migrationBuilder.DropTable(
                name: "BlueprintValidations");

            migrationBuilder.DropTable(
                name: "Evidence",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityGraphNodeIllustrations");

            migrationBuilder.DropTable(
                name: "CapabilityModuleVerification",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Assessment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityPractice",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RecallAttempt",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LearningSessionSteps");

            migrationBuilder.DropTable(
                name: "Goal",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "JobDescription",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PersonProject",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityModule",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Language",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PersonCapability",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LearningSessionNodes");

            migrationBuilder.DropTable(
                name: "Project",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityLevel",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LearningSessions");

            migrationBuilder.DropTable(
                name: "NodeExperienceBlueprints");

            migrationBuilder.DropTable(
                name: "Person",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityGraphNodes");

            migrationBuilder.DropTable(
                name: "Tenant",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityGraphs");

            migrationBuilder.DropTable(
                name: "Capability",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CapabilityDomain",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Subject",
                schema: "dbo");
        }
    }
}
