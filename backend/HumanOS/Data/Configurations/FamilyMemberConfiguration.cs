using HumanOS.Models.Family;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
{
    public void Configure(EntityTypeBuilder<FamilyMember> builder)
    {
        builder.ToTable("FamilyMember", "dbo");

        builder.HasKey(x => x.MemberId)
            .HasName("PK_FamilyMember");

        builder.Property(x => x.MemberId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.FamilyId)
            .IsRequired();

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PreferredName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.FamilyRole)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.RoleTitleEs)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.RoleTitleEn)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.DateOfBirth)
            .HasColumnType("date");

        builder.Property(x => x.EducationLevelEs)
            .HasMaxLength(200);

        builder.Property(x => x.EducationLevelEn)
            .HasMaxLength(200);

        builder.Property(x => x.Institution)
            .HasMaxLength(255);

        builder.Property(x => x.PrimaryLocation)
            .HasMaxLength(255);

        builder.Property(x => x.Email)
            .HasMaxLength(255);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(x => x.PhoneCountryCode)
            .HasMaxLength(10);

        builder.Property(x => x.AzureOid)
            .HasMaxLength(100);

        builder.Property(x => x.AzureLoginEmail)
            .HasMaxLength(255);

        builder.Property(x => x.AzureLinkedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.PlatformRole)
            .HasMaxLength(20)
            .HasDefaultValue("User")
            .IsRequired();

        builder.Property(x => x.AvatarBg)
            .HasMaxLength(100)
            .HasDefaultValue("from-emerald-600 to-teal-700")
            .IsRequired();

        builder.Property(x => x.LanguagesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.TopFocusJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.WeeklyHoursTarget)
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(8.0m)
            .IsRequired();

        builder.Property(x => x.LearningStreakDays)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.TargetMilestoneEs)
            .HasMaxLength(1000);

        builder.Property(x => x.TargetMilestoneEn)
            .HasMaxLength(1000);

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

        builder.HasIndex(x => x.FamilyId)
            .HasDatabaseName("IX_FamilyMember_FamilyId");

        builder.HasOne(x => x.Family)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.FamilyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FamilyMember_FamilyMaster");
    }
}
