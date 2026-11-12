using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// EF configuration for <see cref="StructuredLearningDocumentVisibilityAudit"/>
/// — see that model's doc comment for why this table exists.
/// </summary>
public sealed class StructuredLearningDocumentVisibilityAuditConfiguration
    : IEntityTypeConfiguration<StructuredLearningDocumentVisibilityAudit>
{
    public void Configure(EntityTypeBuilder<StructuredLearningDocumentVisibilityAudit> builder)
    {
        builder.ToTable("StructuredLearningDocumentVisibilityAudit", "dbo");

        builder.HasKey(x => x.StructuredLearningDocumentVisibilityAuditId)
            .HasName("PK_StructuredLearningDocumentVisibilityAudit");

        builder.Property(x => x.StructuredLearningDocumentVisibilityAuditId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ChangedByOid)
            .HasMaxLength(200);

        builder.Property(x => x.ChangedByEmail)
            .HasMaxLength(320);

        builder.HasIndex(x => new { x.StructuredLearningDocumentId, x.ChangedAtUtc });
    }
}
