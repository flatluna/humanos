using HumanOS.Models.JobDescriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class JobDescriptionRecordConfiguration
    : IEntityTypeConfiguration<JobDescriptionRecord>
{
    public void Configure(EntityTypeBuilder<JobDescriptionRecord> builder)
    {
        builder.ToTable("JobDescription", "dbo");

        builder.HasKey(x => x.JobDescriptionId)
            .HasName("PK_JobDescription");

        builder.Property(x => x.JobDescriptionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.SourceStoragePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.SourceFileName)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.SourceUploadedDate)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(x => x.JobTitle)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.RolePurpose)
            .HasMaxLength(2000);

        builder.Property(x => x.RoleSummary)
            .HasMaxLength(4000);

        builder.Property(x => x.PrimaryResponsibilitiesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.ExpectedOutcomesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.RequiredExperience)
            .HasMaxLength(2000);

        builder.Property(x => x.ToolsMentionedJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.SuggestedProfessionalLevel)
            .HasMaxLength(100);

        builder.Property(x => x.ExtractionStatus)
            .HasMaxLength(50)
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(x => x.ExtractionModel)
            .HasMaxLength(100);

        builder.Property(x => x.RawExtractionJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ExtractedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.ConfirmedDate)
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
            .HasConstraintName("FK_JobDescription_Person")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PersonId)
            .HasDatabaseName("IX_JobDescription_PersonId");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_JobDescription_TenantId");
    }
}
