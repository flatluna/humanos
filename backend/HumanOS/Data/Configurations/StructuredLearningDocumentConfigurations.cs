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

        builder.Property(x => x.SessionId)
            .IsRequired();

        builder.Property(x => x.DocumentName)
            .HasMaxLength(500)
            .IsRequired();

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

        builder.Property(x => x.AssessmentQuestionType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(StructuredLearningAssessmentQuestionType.MultipleChoice)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
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

        builder.Property(x => x.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Document)
            .WithMany(d => d.Entities)
            .HasForeignKey(x => x.StructuredLearningDocumentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningEntity_Document");

        // Restrict: Section already cascades from Document — a second
        // cascading path via PrimarySection would create a multiple-
        // cascade-paths error in SQL Server (same reasoning as
        // CapabilityKnowledgeChunkConfiguration).
        builder.HasOne(x => x.PrimarySection)
            .WithMany()
            .HasForeignKey(x => x.PrimarySectionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_StructuredLearningEntity_PrimarySection");

        builder.HasIndex(x => x.StructuredLearningDocumentId)
            .HasDatabaseName("IX_StructuredLearningEntity_DocumentId");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_StructuredLearningEntity_TenantId");
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

        builder.Property(x => x.Evidence)
            .HasColumnType("nvarchar(max)");

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
    }
}
