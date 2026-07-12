using HumanOS.Models.Recall;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class RecallAttemptConfiguration
    : IEntityTypeConfiguration<RecallAttempt>
{
    public void Configure(EntityTypeBuilder<RecallAttempt> builder)
    {
        builder.ToTable("RecallAttempt", "dbo");

        builder.HasKey(x => x.RecallAttemptId)
            .HasName("PK_RecallAttempt");

        builder.Property(x => x.RecallAttemptId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonCapabilityId)
            .IsRequired();

        builder.Property(x => x.RecallPrompt)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.PersonResponse)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.RecallScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.ConfidenceScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.AssistanceLevel)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.LanguageCode)
            .HasMaxLength(10)
            .HasDefaultValue("en")
            .IsRequired();

        builder.Property(x => x.AttemptedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.PersonCapability)
            .WithMany(x => x.RecallAttempts)
            .HasForeignKey(x => x.PersonCapabilityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_RecallAttempt_PersonCapability");

        builder.HasOne(x => x.Language)
            .WithMany(x => x.RecallAttempts)
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_RecallAttempt_Language");

        builder.HasIndex(x => x.PersonCapabilityId)
            .HasDatabaseName(
                "IX_RecallAttempt_PersonCapabilityId");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_RecallAttempt_RecallScore",
                """
                [RecallScore] IS NULL
                OR [RecallScore] BETWEEN 0 AND 100
                """);

            table.HasCheckConstraint(
                "CK_RecallAttempt_ConfidenceScore",
                """
                [ConfidenceScore] IS NULL
                OR [ConfidenceScore] BETWEEN 0 AND 100
                """);

            table.HasCheckConstraint(
                "CK_RecallAttempt_AssistanceLevel",
                "[AssistanceLevel] BETWEEN 0 AND 5");
        });
    }
}
