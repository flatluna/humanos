using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityModuleVerificationConfiguration
    : IEntityTypeConfiguration<CapabilityModuleVerification>
{
    public void Configure(EntityTypeBuilder<CapabilityModuleVerification> builder)
    {
        builder.ToTable("CapabilityModuleVerification", "dbo");

        builder.HasKey(x => x.CapabilityModuleVerificationId)
            .HasName("PK_CapabilityModuleVerification");

        builder.Property(x => x.CapabilityModuleVerificationId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityModuleId)
            .IsRequired();

        builder.Property(x => x.TargetMetric)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Evidence)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.EvidenceLocation)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Explanation)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.RecallStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.RecallEvidence)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.RecallEvidenceLocation)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.RecallOccursBeforeInstruction)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.CapabilityModule)
            .WithMany(x => x.Verifications)
            .HasForeignKey(x => x.CapabilityModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityModuleVerification_CapabilityModule");

        builder.HasIndex(x => x.CapabilityModuleId)
            .HasDatabaseName("IX_CapabilityModuleVerification_CapabilityModuleId");
    }
}
