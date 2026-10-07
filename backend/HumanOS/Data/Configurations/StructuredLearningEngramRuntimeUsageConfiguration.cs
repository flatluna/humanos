using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class StructuredLearningEngramRuntimeUsageConfiguration : IEntityTypeConfiguration<StructuredLearningEngramRuntimeUsage>
{
    public void Configure(EntityTypeBuilder<StructuredLearningEngramRuntimeUsage> builder)
    {
        builder.HasKey(u => u.StructuredLearningEngramRuntimeUsageId);

        builder.Property(u => u.Channel).HasMaxLength(16).IsRequired();
        builder.Property(u => u.ModelName).HasMaxLength(100);
        builder.Property(u => u.StepType).HasMaxLength(32);

        builder.Property(u => u.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasIndex(u => u.PersonId);
        builder.HasIndex(u => u.StructuredLearningDocumentId);
        builder.HasIndex(u => u.StructuredLearningDocumentSectionId);
        builder.HasIndex(u => u.StructuredLearningEngramSessionId);
    }
}
