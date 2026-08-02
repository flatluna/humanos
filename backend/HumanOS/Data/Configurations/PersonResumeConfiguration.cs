using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonResumeConfiguration : IEntityTypeConfiguration<PersonResume>
{
    public void Configure(EntityTypeBuilder<PersonResume> builder)
    {
        builder.ToTable("PersonResume", "dbo");

        builder.HasKey(x => x.PersonResumeId)
            .HasName("PK_PersonResume");

        builder.Property(x => x.PersonResumeId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        builder.Property(x => x.LinkedInUrl)
            .HasMaxLength(500);

        builder.Property(x => x.ProfessionalObjective)
            .HasMaxLength(2000);

        builder.Property(x => x.ProfessionalSummary)
            .HasMaxLength(2000);

        builder.Property(x => x.SkillsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.WorkExperienceJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.EducationJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.CertificationsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.LanguagesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.RawResumeText)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ExtractionModel)
            .HasMaxLength(100);

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
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PersonResume_Person");

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_PersonResume_PersonId");
    }
}
