using System.Text.Json;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// EF configurations for the new TOC-driven "Structured Learning Document"
/// Studio pipeline (Step 1: deterministic Document -&gt; Section extraction,
/// no LLM; Entity/Connection tables exist from the start but stay empty
/// until a future, separate LLM-based step populates them).
/// </summary>
public sealed class StructuredLearningDocumentConfiguration
    : IEntityTypeConfiguration<StructuredLearningDocument>
{
    public void Configure(EntityTypeBuilder<StructuredLearningDocument> builder)
    {
        builder.ToTable("StructuredLearningDocument", "dbo");

        builder.HasKey(x => x.StructuredLearningDocumentId)
            .HasName("PK_StructuredLearningDocument");

        builder.Property(x => x.StructuredLearningDocumentId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.CreatedByOid)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedByEmail)
            .HasMaxLength(320);

        builder.Property(x => x.IsGlobal)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.IsPublished)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.SessionId)
            .IsRequired();

        builder.Property(x => x.DocumentName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.TopicId);

        builder.Property(x => x.PedagogicalAudience)
            .HasMaxLength(30);

        builder.Property(x => x.PromptVersion)
            .HasMaxLength(200);

        builder.Property(x => x.ProcessVersion)
            .HasMaxLength(200);

        builder.Property(x => x.ModelName)
            .HasMaxLength(200);

        builder.Property(x => x.PromptHash)
            .HasMaxLength(128);

        builder.Property(x => x.ConfigSnapshotJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IncludeProductionStep)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IncludeRecallStep)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IncludeAssessmentStep)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IncludeKnowledgeGraphEntities)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IncludeKnowledgeGraphConnections)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.AssessmentQuestionType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(StructuredLearningAssessmentQuestionType.MultipleChoice)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.DetectedDocumentType)
            .HasMaxLength(50);

        builder.Property(x => x.ConfirmedDocumentType)
            .HasMaxLength(50);

        builder.Property(x => x.DocumentTypeConfirmedByUser)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_StructuredLearningDocument_TenantId");

        builder.HasIndex(x => x.SessionId)
            .HasDatabaseName("IX_StructuredLearningDocument_SessionId");
    }
}

public sealed class StructuredLearningDocumentSectionConfiguration
    : IEntityTypeConfiguration<StructuredLearningDocumentSection>
{
    public void Configure(EntityTypeBuilder<StructuredLearningDocumentSection> builder)
    {
        builder.ToTable("StructuredLearningDocumentSection", "dbo");

        builder.HasKey(x => x.StructuredLearningDocumentSectionId)
            .HasName("PK_StructuredLearningDocumentSection");

        builder.Property(x => x.StructuredLearningDocumentSectionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.ChapterTitle)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Capability)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.SubchapterTitle)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Text)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.TotalCharacters)
            .IsRequired();

        builder.Property(x => x.TotalTokens)
            .IsRequired();

        builder.Property(x => x.Embedding)
            .HasColumnType("vector(1536)")
            .IsRequired();

        builder.Property(x => x.PedagogyStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(StructuredLearningPedagogyStatus.NotStarted)
            .IsRequired();

        builder.Property(x => x.PedagogyErrorMessage)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.PromptVersion)
            .HasMaxLength(200);

        builder.Property(x => x.ProcessVersion)
            .HasMaxLength(200);

        builder.Property(x => x.ModelName)
            .HasMaxLength(200);

        builder.Property(x => x.PromptHash)
            .HasMaxLength(128);

        builder.Property(x => x.ConfigSnapshotJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Statements)
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value ?? new List<StructuredLearningStatement>(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                json => string.IsNullOrWhiteSpace(json)
                    ? new List<StructuredLearningStatement>()
                    : JsonSerializer.Deserialize<List<StructuredLearningStatement>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<StructuredLearningStatement>());

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Document)
            .WithMany(d => d.Sections)
            .HasForeignKey(x => x.StructuredLearningDocumentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningDocumentSection_Document");

        builder.HasIndex(x => x.StructuredLearningDocumentId)
            .HasDatabaseName("IX_StructuredLearningDocumentSection_DocumentId");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_StructuredLearningDocumentSection_TenantId");
    }
}

/// <summary>Knowledge Graph — Catálogo de Tipos/Temas (2026-08-16). See
/// <see cref="StructuredLearningTopic"/>'s own doc comment.</summary>
public sealed class StructuredLearningTopicConfiguration
    : IEntityTypeConfiguration<StructuredLearningTopic>
{
    public void Configure(EntityTypeBuilder<StructuredLearningTopic> builder)
    {
        builder.ToTable("StructuredLearningTopic", "dbo");

        builder.HasKey(x => x.StructuredLearningTopicId)
            .HasName("PK_StructuredLearningTopic");

        builder.Property(x => x.StructuredLearningTopicId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2");

        builder.HasIndex(x => new { x.TenantId, x.SubjectId })
            .HasDatabaseName("IX_StructuredLearningTopic_TenantId_SubjectId");
    }
}

public sealed class StructuredLearningEntityConfiguration
    : IEntityTypeConfiguration<StructuredLearningEntity>
{
    public void Configure(EntityTypeBuilder<StructuredLearningEntity> builder)
    {
        builder.ToTable("StructuredLearningEntity", "dbo");

        builder.HasKey(x => x.StructuredLearningEntityId)
            .HasName("PK_StructuredLearningEntity");

        builder.Property(x => x.StructuredLearningEntityId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubjectId);

        builder.Property(x => x.TopicId);

        builder.Property(x => x.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityClassId);

        builder.HasOne<StructuredLearningEntityClass>()
            .WithMany()
            .HasForeignKey(x => x.EntityClassId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_StructuredLearningEntity_EntityClass");

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.OntologicalClass)
            .HasMaxLength(150);

        builder.Property(x => x.NormalizedValue)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Aliases)
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value ?? new List<string>(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                json => string.IsNullOrWhiteSpace(json)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<string>());

        builder.Property(x => x.Embedding)
            .HasColumnType("vector(1536)");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        // Deduplication key: one canonical row per (tenant, normalized
        // value, type) — StructuredLearningEntityExtractionService always
        // looks this up before inserting, making extraction idempotent.
        builder.HasIndex(x => new { x.TenantId, x.NormalizedValue, x.EntityType })
            .IsUnique()
            .HasDatabaseName("IX_StructuredLearningEntity_TenantId_NormalizedValue_EntityType");

        // Master registry lookup (2026-08-15) — the set an extraction agent
        // is offered as reusable candidates for one domain.
        builder.HasIndex(x => new { x.TenantId, x.SubjectId })
            .HasDatabaseName("IX_StructuredLearningEntity_TenantId_SubjectId");
    }
}

/// <summary>Knowledge Graph \u2014 Master de Clases de Entidad (2026-08-14, Fase 1). See
/// <see cref="StructuredLearningEntityClass"/>'s own doc comment.</summary>
public sealed class StructuredLearningEntityClassConfiguration
    : IEntityTypeConfiguration<StructuredLearningEntityClass>
{
    public void Configure(EntityTypeBuilder<StructuredLearningEntityClass> builder)
    {
        builder.ToTable("StructuredLearningEntityClass", "dbo");

        builder.HasKey(x => x.StructuredLearningEntityClassId)
            .HasName("PK_StructuredLearningEntityClass");

        builder.Property(x => x.StructuredLearningEntityClassId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.DisplayLabel)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.GenericDescription)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.NormalizedName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        // Deduplication key: one class per (tenant, domain/subject,
        // normalized name) — the extraction agent is given this catalog
        // and must reuse an existing class instead of creating a duplicate.
        builder.HasIndex(x => new { x.TenantId, x.SubjectId, x.NormalizedName })
            .IsUnique()
            .HasDatabaseName("IX_StructuredLearningEntityClass_TenantId_SubjectId_NormalizedName");
    }
}

/// <summary>Knowledge Graph \u2014 Master de Clases de Arista (2026-08-15, Fase 3). See
/// <see cref="StructuredLearningRelationshipClass"/>'s own doc comment.</summary>
public sealed class StructuredLearningRelationshipClassConfiguration
    : IEntityTypeConfiguration<StructuredLearningRelationshipClass>
{
    public void Configure(EntityTypeBuilder<StructuredLearningRelationshipClass> builder)
    {
        builder.ToTable("StructuredLearningRelationshipClass", "dbo");

        builder.HasKey(x => x.StructuredLearningRelationshipClassId)
            .HasName("PK_StructuredLearningRelationshipClass");

        builder.Property(x => x.StructuredLearningRelationshipClassId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .IsRequired();

        builder.Property(x => x.Label)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(50)
            .HasDefaultValue("Generic")
            .IsRequired();

        builder.Property(x => x.GenericDescription)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.NormalizedLabel)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.SubjectId, x.NormalizedLabel })
            .IsUnique()
            .HasDatabaseName("IX_StructuredLearningRelationshipClass_TenantId_SubjectId_NormalizedLabel");
    }
}

public sealed class StructuredLearningEntitySectionConfiguration
    : IEntityTypeConfiguration<StructuredLearningEntitySection>
{
    public void Configure(EntityTypeBuilder<StructuredLearningEntitySection> builder)
    {
        builder.ToTable("StructuredLearningEntitySection", "dbo");

        builder.HasKey(x => x.StructuredLearningEntitySectionId)
            .HasName("PK_StructuredLearningEntitySection");

        builder.Property(x => x.StructuredLearningEntitySectionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SourceText)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.ResolutionStatus)
            .HasMaxLength(20)
            .HasDefaultValue("new")
            .IsRequired();

        builder.Property(x => x.MatchReason)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Entity)
            .WithMany(e => e.Occurrences)
            .HasForeignKey(x => x.StructuredLearningEntityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningEntitySection_Entity");

        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.StructuredLearningDocumentSectionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningEntitySection_Section");

        // Idempotency: re-running extraction for a section never creates a
        // second link row for the same entity.
        builder.HasIndex(x => new { x.StructuredLearningEntityId, x.StructuredLearningDocumentSectionId })
            .IsUnique()
            .HasDatabaseName("IX_StructuredLearningEntitySection_EntityId_SectionId");

        builder.HasIndex(x => x.StructuredLearningDocumentSectionId)
            .HasDatabaseName("IX_StructuredLearningEntitySection_SectionId");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_StructuredLearningEntitySection_TenantId");
    }
}

public sealed class StructuredLearningGenerationUsageConfiguration
    : IEntityTypeConfiguration<StructuredLearningGenerationUsage>
{
    public void Configure(EntityTypeBuilder<StructuredLearningGenerationUsage> builder)
    {
        builder.ToTable("StructuredLearningGenerationUsage", "dbo");

        builder.HasKey(x => x.StructuredLearningGenerationUsageId)
            .HasName("PK_StructuredLearningGenerationUsage");

        builder.Property(x => x.StructuredLearningGenerationUsageId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.StepName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.AgentName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ModelName)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        // No FK — the document/section may already be gone by the time
        // this row's cost is analyzed, and a usage row must never block or
        // cascade-delete an unrelated document (same rationale as
        // CapabilityGenerationUsage never blocking Capability deletion
        // being a concern worth avoiding).
        builder.HasIndex(x => x.StructuredLearningDocumentId)
            .HasDatabaseName("IX_StructuredLearningGenerationUsage_DocumentId");

        builder.HasIndex(x => x.StructuredLearningDocumentSectionId)
            .HasDatabaseName("IX_StructuredLearningGenerationUsage_SectionId");
    }
}

public sealed class StructuredLearningConnectionConfiguration
    : IEntityTypeConfiguration<StructuredLearningConnection>
{
    public void Configure(EntityTypeBuilder<StructuredLearningConnection> builder)
    {
        builder.ToTable("StructuredLearningConnection", "dbo");

        builder.HasKey(x => x.StructuredLearningConnectionId)
            .HasName("PK_StructuredLearningConnection");

        builder.Property(x => x.StructuredLearningConnectionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.RelationshipLabel)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.RelationshipCategory)
            .HasMaxLength(50)
            .HasDefaultValue("Generic")
            .IsRequired();

        builder.Property(x => x.Evidence)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.EpistemicStatus)
            .HasMaxLength(20)
            .HasDefaultValue("explicit")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Document)
            .WithMany(d => d.Connections)
            .HasForeignKey(x => x.StructuredLearningDocumentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningConnection_Document");

        // Restrict on all 3: Entity/Section already cascade from Document —
        // cascading these too would create multiple-cascade-paths errors.
        builder.HasOne(x => x.SourceEntity)
            .WithMany()
            .HasForeignKey(x => x.SourceEntityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_StructuredLearningConnection_SourceEntity");

        builder.HasOne(x => x.TargetEntity)
            .WithMany()
            .HasForeignKey(x => x.TargetEntityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_StructuredLearningConnection_TargetEntity");

        builder.HasOne(x => x.SourceSection)
            .WithMany()
            .HasForeignKey(x => x.SourceSectionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_StructuredLearningConnection_SourceSection");

        builder.HasIndex(x => x.StructuredLearningDocumentId)
            .HasDatabaseName("IX_StructuredLearningConnection_DocumentId");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_StructuredLearningConnection_TenantId");

        builder.HasIndex(x => x.SourceEntityId)
            .HasDatabaseName("IX_StructuredLearningConnection_SourceEntityId");

        builder.HasIndex(x => x.TargetEntityId)
            .HasDatabaseName("IX_StructuredLearningConnection_TargetEntityId");
    }
}

public sealed class StructuredLearningKnowledgeFactConfiguration
    : IEntityTypeConfiguration<StructuredLearningKnowledgeFact>
{
    public void Configure(EntityTypeBuilder<StructuredLearningKnowledgeFact> builder)
    {
        builder.ToTable("KnowledgeFacts", "dbo", table =>
        {
            table.HasCheckConstraint("CK_KnowledgeFacts_FactType",
                "[FactType] IN ('Definition','Structure','Relationship','Principle','Capability','Process','Function','Outcome','Application')");
            table.HasCheckConstraint("CK_KnowledgeFacts_Importance",
                "[Importance] IN ('Core','Supporting')");
            table.HasCheckConstraint("CK_KnowledgeFacts_DerivationType",
                "[DerivationType] IN ('Atomic','Composite')");
            table.HasCheckConstraint("CK_KnowledgeFacts_EpistemicStatus",
                "[EpistemicStatus] IN ('explicit','inferred')");
            table.HasCheckConstraint("CK_KnowledgeFacts_Confidence",
                "[Confidence] >= 0 AND [Confidence] <= 1");
            table.HasCheckConstraint("CK_KnowledgeFacts_Statement_NotEmpty",
                "LEN(LTRIM(RTRIM([Statement]))) > 0");
        });

        builder.HasKey(x => x.KnowledgeFactId)
            .HasName("PK_KnowledgeFacts");

        builder.Property(x => x.KnowledgeFactId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.EngramId)
            .IsRequired();

        builder.Property(x => x.Statement)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.FactType)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Importance)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.DerivationType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.EpistemicStatus)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Confidence)
            .HasColumnType("float")
            .IsRequired();

        builder.Property(x => x.Language)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("datetime2");

        builder.HasOne(x => x.Engram)
            .WithMany()
            .HasForeignKey(x => x.EngramId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_KnowledgeFacts_Engram");

        builder.HasIndex(x => x.EngramId)
            .HasDatabaseName("IX_KnowledgeFacts_EngramId");

        builder.HasIndex(x => new { x.EngramId, x.Importance })
            .HasDatabaseName("IX_KnowledgeFacts_EngramId_Importance");
    }
}

/// <summary>Step 3 output — see <see cref="StructuredLearningSectionStep"/>.</summary>
public sealed class StructuredLearningSectionStepConfiguration
    : IEntityTypeConfiguration<StructuredLearningSectionStep>
{
    public void Configure(EntityTypeBuilder<StructuredLearningSectionStep> builder)
    {
        builder.ToTable("StructuredLearningSectionStep", "dbo");

        builder.HasKey(x => x.StructuredLearningSectionStepId)
            .HasName("PK_StructuredLearningSectionStep");

        builder.Property(x => x.StructuredLearningSectionStepId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.StepType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.StepDataJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CurrentRecallPrompt)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CurrentDeepDiveCardIndex)
            .IsRequired();

        builder.Property(x => x.CurrentDeepDivePrompt)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.PromptVersion)
            .HasMaxLength(200);

        builder.Property(x => x.ProcessVersion)
            .HasMaxLength(200);

        builder.Property(x => x.ModelName)
            .HasMaxLength(200);

        builder.Property(x => x.PromptHash)
            .HasMaxLength(128);

        builder.Property(x => x.ConfigSnapshotJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IsHumanApproved)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.HumanApprovedAtUtc);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Section)
            .WithMany(s => s.Steps)
            .HasForeignKey(x => x.StructuredLearningDocumentSectionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningSectionStep_Section");

        builder.HasIndex(x => x.StructuredLearningDocumentSectionId)
            .HasDatabaseName("IX_StructuredLearningSectionStep_SectionId");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_StructuredLearningSectionStep_TenantId");
    }
}

public sealed class StructuredLearningKnowledgeFactGraphPathStepConfiguration
    : IEntityTypeConfiguration<StructuredLearningKnowledgeFactGraphPathStep>
{
    public void Configure(EntityTypeBuilder<StructuredLearningKnowledgeFactGraphPathStep> builder)
    {
        builder.ToTable("KnowledgeFactGraphPathSteps", "dbo", table =>
        {
            table.HasCheckConstraint("CK_KnowledgeFactGraphPathSteps_StepOrder", "[StepOrder] >= 1 AND [StepOrder] <= 3");
        });

        builder.HasKey(x => x.KnowledgeFactGraphPathStepId)
            .HasName("PK_KnowledgeFactGraphPathSteps");

        builder.Property(x => x.KnowledgeFactGraphPathStepId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.KnowledgeFactId)
            .IsRequired();

        builder.Property(x => x.ConnectionId)
            .IsRequired();

        builder.Property(x => x.StepOrder)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.KnowledgeFact)
            .WithMany(x => x.GraphPathSteps)
            .HasForeignKey(x => x.KnowledgeFactId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_KnowledgeFactGraphPathSteps_KnowledgeFacts");

        builder.HasOne(x => x.Connection)
            .WithMany()
            .HasForeignKey(x => x.ConnectionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_KnowledgeFactGraphPathSteps_Connection");

        builder.HasIndex(x => new { x.KnowledgeFactId, x.StepOrder })
            .IsUnique()
            .HasDatabaseName("UX_KnowledgeFactGraphPathSteps_KnowledgeFactId_StepOrder");

        builder.HasIndex(x => new { x.KnowledgeFactId, x.ConnectionId })
            .IsUnique()
            .HasDatabaseName("UX_KnowledgeFactGraphPathSteps_KnowledgeFactId_ConnectionId");

        builder.HasIndex(x => x.ConnectionId)
            .HasDatabaseName("IX_KnowledgeFactGraphPathSteps_ConnectionId");
    }
}

/// <summary>Step 3 DeepDive card illustration metadata — see
/// <see cref="StructuredLearningIllustration"/>.</summary>
public sealed class StructuredLearningIllustrationConfiguration
    : IEntityTypeConfiguration<StructuredLearningIllustration>
{
    public void Configure(EntityTypeBuilder<StructuredLearningIllustration> builder)
    {
        builder.ToTable("StructuredLearningIllustration", "dbo");

        builder.HasKey(x => x.StructuredLearningIllustrationId)
            .HasName("PK_StructuredLearningIllustration");

        builder.Property(x => x.StructuredLearningIllustrationId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.StoragePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Prompt)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.MediaType)
            .HasMaxLength(20)
            .HasDefaultValue("Image")
            .IsRequired();

        builder.Property(x => x.PromptVersion)
            .HasMaxLength(200);

        builder.Property(x => x.ProcessVersion)
            .HasMaxLength(200);

        builder.Property(x => x.PromptHash)
            .HasMaxLength(128);

        builder.Property(x => x.ConfigSnapshotJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Caption)
            .HasMaxLength(2000);

        builder.Property(x => x.Purpose)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ImageModel)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => x.StructuredLearningDocumentSectionId)
            .HasDatabaseName("IX_StructuredLearningIllustration_SectionId");
    }
}

/// <summary>Real-time Assessment (2026-08-10) — see
/// <see cref="StructuredLearningAssessmentRound"/>.</summary>
public sealed class StructuredLearningAssessmentRoundConfiguration
    : IEntityTypeConfiguration<StructuredLearningAssessmentRound>
{
    public void Configure(EntityTypeBuilder<StructuredLearningAssessmentRound> builder)
    {
        builder.ToTable("StructuredLearningAssessmentRound", "dbo");

        builder.HasKey(x => x.StructuredLearningAssessmentRoundId)
            .HasName("PK_StructuredLearningAssessmentRound");

        builder.Property(x => x.StructuredLearningAssessmentRoundId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.RoundNumber)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.StructuredLearningDocumentSectionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningAssessmentRound_Section");

        builder.HasIndex(x => x.StructuredLearningDocumentSectionId)
            .HasDatabaseName("IX_StructuredLearningAssessmentRound_SectionId");

        builder.HasIndex(x => new { x.PersonId, x.StructuredLearningDocumentSectionId })
            .HasDatabaseName("IX_StructuredLearningAssessmentRound_Person_SectionId");
    }
}

/// <summary>Real-time Assessment (2026-08-10) — see
/// <see cref="StructuredLearningAssessmentQuestionAttempt"/>.</summary>
public sealed class StructuredLearningAssessmentQuestionAttemptConfiguration
    : IEntityTypeConfiguration<StructuredLearningAssessmentQuestionAttempt>
{
    public void Configure(EntityTypeBuilder<StructuredLearningAssessmentQuestionAttempt> builder)
    {
        builder.ToTable("StructuredLearningAssessmentQuestionAttempt", "dbo");

        builder.HasKey(x => x.StructuredLearningAssessmentQuestionAttemptId)
            .HasName("PK_StructuredLearningAssessmentQuestionAttempt");

        builder.Property(x => x.StructuredLearningAssessmentQuestionAttemptId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.QuestionIndex)
            .IsRequired();

        builder.Property(x => x.QuestionText)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.OptionsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.StudentAnswer)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Correctness)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Feedback)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ObservedError)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Round)
            .WithMany(r => r.Questions)
            .HasForeignKey(x => x.StructuredLearningAssessmentRoundId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningAssessmentQuestionAttempt_Round");

        builder.HasIndex(x => x.StructuredLearningAssessmentRoundId)
            .HasDatabaseName("IX_StructuredLearningAssessmentQuestionAttempt_RoundId");
    }
}

/// <summary>Real-time Recall (2026-08-10) — see
/// <see cref="StructuredLearningRecallAttempt"/>.</summary>
public sealed class StructuredLearningRecallAttemptConfiguration
    : IEntityTypeConfiguration<StructuredLearningRecallAttempt>
{
    public void Configure(EntityTypeBuilder<StructuredLearningRecallAttempt> builder)
    {
        builder.ToTable("StructuredLearningRecallAttempt", "dbo");

        builder.HasKey(x => x.StructuredLearningRecallAttemptId)
            .HasName("PK_StructuredLearningRecallAttempt");

        builder.Property(x => x.StructuredLearningRecallAttemptId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TutorPromptShown)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.StudentResponse)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.RecallScore)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.StructuredLearningDocumentSectionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningRecallAttempt_Section");

        builder.HasIndex(x => x.StructuredLearningDocumentSectionId)
            .HasDatabaseName("IX_StructuredLearningRecallAttempt_SectionId");

        builder.HasIndex(x => new { x.PersonId, x.StructuredLearningDocumentSectionId })
            .HasDatabaseName("IX_StructuredLearningRecallAttempt_Person_SectionId");
    }
}

/// <summary>Per-student runtime state (2026-08-13) — see
/// <see cref="StructuredLearningPersonSectionState"/>.</summary>
public sealed class StructuredLearningPersonSectionStateConfiguration
    : IEntityTypeConfiguration<StructuredLearningPersonSectionState>
{
    public void Configure(EntityTypeBuilder<StructuredLearningPersonSectionState> builder)
    {
        builder.ToTable("StructuredLearningPersonSectionState", "dbo");

        builder.HasKey(x => x.StructuredLearningPersonSectionStateId)
            .HasName("PK_StructuredLearningPersonSectionState");

        builder.Property(x => x.StructuredLearningPersonSectionStateId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.CurrentRecallPrompt)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CurrentDeepDiveCardIndex)
            .IsRequired();

        builder.Property(x => x.CurrentDeepDivePrompt)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.StructuredLearningDocumentSectionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningPersonSectionState_Section");

        builder.HasIndex(x => new { x.PersonId, x.StructuredLearningDocumentSectionId })
            .IsUnique()
            .HasDatabaseName("UX_StructuredLearningPersonSectionState_Person_SectionId");
    }
}

/// <summary>Per-student Hypothesis pre-check answers (2026-08-13) — see
/// <see cref="StructuredLearningHypothesisResponse"/>.</summary>
public sealed class StructuredLearningHypothesisResponseConfiguration
    : IEntityTypeConfiguration<StructuredLearningHypothesisResponse>
{
    public void Configure(EntityTypeBuilder<StructuredLearningHypothesisResponse> builder)
    {
        builder.ToTable("StructuredLearningHypothesisResponse", "dbo");

        builder.HasKey(x => x.StructuredLearningHypothesisResponseId)
            .HasName("PK_StructuredLearningHypothesisResponse");

        builder.Property(x => x.StructuredLearningHypothesisResponseId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.QuestionIndex)
            .IsRequired();

        builder.Property(x => x.SelectedOptionIndex)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.StructuredLearningDocumentSectionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningHypothesisResponse_Section");

        builder.HasIndex(x => new { x.PersonId, x.StructuredLearningDocumentSectionId })
            .HasDatabaseName("IX_StructuredLearningHypothesisResponse_Person_SectionId");
    }
}
