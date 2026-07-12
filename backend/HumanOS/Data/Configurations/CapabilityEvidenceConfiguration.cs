using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityEvidenceConfiguration
    : IEntityTypeConfiguration<CapabilityEvidence>
{
    public void Configure(
        EntityTypeBuilder<CapabilityEvidence> builder)
    {
        builder.ToTable("CapabilityEvidence", "dbo");

        builder.HasKey(x => x.CapabilityEvidenceId)
            .HasName("PK_CapabilityEvidence");

        builder.Property(x => x.CapabilityEvidenceId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.PersonCapabilityId)
            .IsRequired();

        builder.Property(x => x.EvidenceId)
            .IsRequired();

        builder.Property(x => x.EvidenceType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ContributionWeight)
            .HasPrecision(5, 2);

        builder.Property(x => x.ValidationStatus)
            .HasMaxLength(30)
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(x => x.ValidatedByPersonId);

        builder.Property(x => x.ValidatedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.PersonCapability)
            .WithMany()
            .HasForeignKey(x => x.PersonCapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_CapabilityEvidence_PersonCapability");

        builder.HasOne(x => x.Evidence)
            .WithMany()
            .HasForeignKey(x => x.EvidenceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_CapabilityEvidence_Evidence");

        builder.HasOne(x => x.ValidatedByPerson)
            .WithMany()
            .HasForeignKey(x => x.ValidatedByPersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_CapabilityEvidence_Validator");

        builder.HasIndex(x => new
        {
            x.PersonCapabilityId,
            x.EvidenceId
        })
            .IsUnique()
            .HasDatabaseName("UQ_CapabilityEvidence");
    }
}
