using HumanOS.Models.Capabilities.Studio;
using HumanOS.Models.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class StructuredLearningDocumentProgramMembershipConfiguration : IEntityTypeConfiguration<StructuredLearningDocumentProgramMembership>
{
    public void Configure(EntityTypeBuilder<StructuredLearningDocumentProgramMembership> builder)
    {
        builder.ToTable("StructuredLearningDocumentProgramMembership", "dbo");

        builder.HasKey(x => x.StructuredLearningDocumentProgramMembershipId)
            .HasName("PK_StructuredLearningDocumentProgramMembership");

        builder.Property(x => x.StructuredLearningDocumentProgramMembershipId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Program)
            .WithMany()
            .HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningDocumentProgramMembership_Program");

        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.StructuredLearningDocumentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StructuredLearningDocumentProgramMembership_Document");

        builder.HasIndex(x => new { x.ProgramId, x.StructuredLearningDocumentId })
            .IsUnique()
            .HasDatabaseName("UX_StructuredLearningDocumentProgramMembership_Program_Document");

        builder.HasIndex(x => new { x.ProgramId, x.SortOrder })
            .IsUnique()
            .HasDatabaseName("UX_StructuredLearningDocumentProgramMembership_Program_SortOrder");
    }
}
