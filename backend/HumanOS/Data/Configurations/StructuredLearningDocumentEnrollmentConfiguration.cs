using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class StructuredLearningDocumentEnrollmentConfiguration : IEntityTypeConfiguration<StructuredLearningDocumentEnrollment>
{
    public void Configure(EntityTypeBuilder<StructuredLearningDocumentEnrollment> builder)
    {
        builder.ToTable("StructuredLearningDocumentEnrollment", "dbo");

        builder.HasKey(x => x.StructuredLearningDocumentEnrollmentId)
            .HasName("PK_StructuredLearningDocumentEnrollment");

        builder.Property(x => x.StructuredLearningDocumentEnrollmentId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.StructuredLearningDocumentId)
            .IsRequired();

        builder.Property(x => x.EnrolledDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.StructuredLearningDocumentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PersonId, x.StructuredLearningDocumentId })
            .IsUnique()
            .HasDatabaseName("UX_StructuredLearningDocumentEnrollment_Person_Document");
    }
}
