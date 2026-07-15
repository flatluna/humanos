using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonCapabilityConfiguration
    : IEntityTypeConfiguration<PersonCapability>
{
    public void Configure(
        EntityTypeBuilder<PersonCapability> builder)
    {
        builder.ToTable("PersonCapability", "dbo");

        builder.HasKey(x => x.PersonCapabilityId)
            .HasName("PK_PersonCapability");

        builder.Property(x => x.PersonCapabilityId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.CurrentLevel)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.TargetLevel)
            .HasDefaultValue(5)
            .IsRequired();

        builder.Property(x => x.ProgressPercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.MasteryScore)
            .HasPrecision(5, 2)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .HasDefaultValue("NotStarted")
            .IsRequired();

        builder.Property(x => x.IndependenceLevel)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.RetentionScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.ConfidenceScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.KnowledgeScore)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.RecallScore)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.ApplicationScore)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.StartedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.LastActivityDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_PersonCapability_Person");

        builder.HasOne(x => x.Capability)
            .WithMany(x => x.PersonCapabilities)
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_PersonCapability_Capability");

        builder.HasIndex(x => new
        {
            x.PersonId,
            x.CapabilityId
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_PersonCapability_PersonId_CapabilityId");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_PersonCapability_CurrentLevel",
                "[CurrentLevel] BETWEEN 0 AND 5");

            table.HasCheckConstraint(
                "CK_PersonCapability_TargetLevel",
                "[TargetLevel] BETWEEN 0 AND 5");

            table.HasCheckConstraint(
                "CK_PersonCapability_ProgressPercentage",
                "[ProgressPercentage] BETWEEN 0 AND 100");

            table.HasCheckConstraint(
                "CK_PersonCapability_MasteryScore",
                "[MasteryScore] BETWEEN 0 AND 100");

            table.HasCheckConstraint(
                "CK_PersonCapability_Status",
                """
                [Status] IN
                ('NotStarted', 'InProgress', 'Paused', 'Completed')
                """);

            table.HasCheckConstraint(
                "CK_PersonCapability_IndependenceLevel",
                "[IndependenceLevel] BETWEEN 0 AND 5");

            table.HasCheckConstraint(
                "CK_PersonCapability_RetentionScore",
                """
                [RetentionScore] IS NULL
                OR [RetentionScore] BETWEEN 0 AND 100
                """);

            table.HasCheckConstraint(
                "CK_PersonCapability_ConfidenceScore",
                """
                [ConfidenceScore] IS NULL
                OR [ConfidenceScore] BETWEEN 0 AND 100
                """);
        });
    }
}
