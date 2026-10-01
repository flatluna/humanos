using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonAcademicPreferencesConfiguration : IEntityTypeConfiguration<PersonAcademicPreferences>
{
    public void Configure(EntityTypeBuilder<PersonAcademicPreferences> builder)
    {
        builder.ToTable("PersonAcademicPreferences", "dbo");

        builder.HasKey(x => x.PersonAcademicPreferencesId)
            .HasName("PK_PersonAcademicPreferences");

        builder.Property(x => x.PersonAcademicPreferencesId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.AcademicInterestsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.LearningPreferencesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.AcademicMotivationsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.GrowthAreasJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.Frustrations)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.MainChallenges)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ImportantValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithOne()
            .HasForeignKey<PersonAcademicPreferences>(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PersonAcademicPreferences_Person");

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_PersonAcademicPreferences_PersonId");
    }
}
