using HumanOS.Models.Family;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class FamilyMemberEducationConfiguration : IEntityTypeConfiguration<FamilyMemberEducation>
{
    public void Configure(EntityTypeBuilder<FamilyMemberEducation> builder)
    {
        builder.ToTable("FamilyMemberEducation", "dbo");

        builder.HasKey(x => x.EducationId)
            .HasName("PK_FamilyMemberEducation");

        builder.Property(x => x.EducationId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.FamilyId)
            .IsRequired();

        builder.Property(x => x.MemberId)
            .IsRequired();

        builder.Property(x => x.EducationLevel)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SchoolName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.GradeOrDegree)
            .HasMaxLength(150);

        builder.Property(x => x.FieldOfStudy)
            .HasMaxLength(200);

        builder.Property(x => x.City)
            .HasMaxLength(200);

        builder.Property(x => x.State)
            .HasMaxLength(100);

        builder.Property(x => x.Country)
            .HasMaxLength(100);

        builder.Property(x => x.Gpa)
            .HasMaxLength(20);

        builder.Property(x => x.Degree)
            .HasMaxLength(150);

        builder.Property(x => x.Status)
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.IsCurrent)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_FamilyMemberEducation_TenantId");

        builder.HasIndex(x => x.FamilyId)
            .HasDatabaseName("IX_FamilyMemberEducation_FamilyId");

        builder.HasIndex(x => x.MemberId)
            .HasDatabaseName("IX_FamilyMemberEducation_MemberId");

        builder.HasOne(x => x.Family)
            .WithMany()
            .HasForeignKey(x => x.FamilyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FamilyMemberEducation_FamilyMaster");

        builder.HasOne(x => x.Member)
            .WithMany()
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FamilyMemberEducation_FamilyMember");
    }
}
