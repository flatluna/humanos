using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityModuleSuccessCriterionResultConfiguration
    : IEntityTypeConfiguration<CapabilityModuleSuccessCriterionResult>
{
    public void Configure(EntityTypeBuilder<CapabilityModuleSuccessCriterionResult> builder)
    {
        builder.ToTable("CapabilityModuleSuccessCriterionResult", "dbo");

        builder.HasKey(x => x.CapabilityModuleSuccessCriterionResultId)
            .HasName("PK_CapabilityModuleSuccessCriterionResult");

        builder.Property(x => x.CapabilityModuleSuccessCriterionResultId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityModuleVerificationId)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.Criterion)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.IsSatisfied)
            .IsRequired();

        builder.Property(x => x.Evidence)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.HasOne(x => x.CapabilityModuleVerification)
            .WithMany(x => x.SuccessCriteriaResults)
            .HasForeignKey(x => x.CapabilityModuleVerificationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityModuleSuccessCriterionResult_CapabilityModuleVerification");

        builder.HasIndex(x => x.CapabilityModuleVerificationId)
            .HasDatabaseName("IX_CapabilityModuleSuccessCriterionResult_CapabilityModuleVerificationId");
    }
}
