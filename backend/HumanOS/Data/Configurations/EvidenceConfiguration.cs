using HumanOS.Models.Evidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class EvidenceConfiguration
    : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> builder)
    {
        builder.ToTable("Evidence", "dbo");

        builder.HasKey(x => x.EvidenceId)
            .HasName("PK_Evidence");

        builder.Property(x => x.EvidenceId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.PersonProjectId);

        builder.Property(x => x.Title)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.EvidenceType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EvidenceUrl)
            .HasMaxLength(2000);

        builder.Property(x => x.ValidationStatus)
            .HasMaxLength(30)
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(x => x.AssistanceLevel)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.ValidationFeedback)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ValidatedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.SubmittedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Evidence_Person");

        builder.HasOne(x => x.Capability)
            .WithMany()
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Evidence_Capability");

        builder.HasOne(x => x.PersonProject)
            .WithMany()
            .HasForeignKey(x => x.PersonProjectId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Evidence_PersonProject");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Evidence_ValidationStatus",
                """
                [ValidationStatus] IN
                (
                    'Pending',
                    'Accepted',
                    'Rejected'
                )
                """);

            table.HasCheckConstraint(
                "CK_Evidence_AssistanceLevel",
                "[AssistanceLevel] BETWEEN 0 AND 5");

            table.HasCheckConstraint(
                "CK_Evidence_ValidationState",
                """
                (
                    [ValidationStatus] = 'Pending'
                    AND [ValidatedDate] IS NULL
                )
                OR
                (
                    [ValidationStatus] IN ('Accepted', 'Rejected')
                    AND [ValidatedDate] IS NOT NULL
                )
                """);
        });
    }
}
