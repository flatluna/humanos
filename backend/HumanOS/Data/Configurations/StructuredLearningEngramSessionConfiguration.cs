using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class StructuredLearningEngramSessionConfiguration : IEntityTypeConfiguration<StructuredLearningEngramSession>
{
    public void Configure(EntityTypeBuilder<StructuredLearningEngramSession> builder)
    {
        builder.HasKey(s => s.StructuredLearningEngramSessionId);

        builder.Property(s => s.StartedDate).HasDefaultValueSql("GETUTCDATE()").IsRequired();
        builder.Property(s => s.LastActivityDate).HasDefaultValueSql("GETUTCDATE()").IsRequired();

        builder.HasIndex(s => s.PersonId);
        builder.HasIndex(s => s.StructuredLearningDocumentId);
        builder.HasIndex(s => s.StructuredLearningDocumentSectionId);

        builder.HasMany(s => s.Steps)
            .WithOne(st => st.Session)
            .HasForeignKey(st => st.StructuredLearningEngramSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StructuredLearningEngramSessionStepConfiguration : IEntityTypeConfiguration<StructuredLearningEngramSessionStep>
{
    public void Configure(EntityTypeBuilder<StructuredLearningEngramSessionStep> builder)
    {
        builder.HasKey(s => s.StructuredLearningEngramSessionStepId);

        builder.Property(s => s.StepType).HasMaxLength(32).IsRequired();
        builder.Property(s => s.StartedDate).HasDefaultValueSql("GETUTCDATE()").IsRequired();

        builder.HasIndex(s => s.StructuredLearningEngramSessionId);
        builder.HasIndex(s => s.StructuredLearningDocumentSectionId);
    }
}
