using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonProfileConfiguration
    : IEntityTypeConfiguration<PersonProfile>
{
    public void Configure(EntityTypeBuilder<PersonProfile> builder)
    {
        builder.ToTable("PersonProfile", "dbo");

        builder.HasKey(x => x.PersonProfileId)
            .HasName("PK_PersonProfile");

        builder.Property(x => x.PersonProfileId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasMaxLength(200);

        builder.Property(x => x.LastName)
            .HasMaxLength(200);

        builder.Property(x => x.DisplayName)
            .HasMaxLength(400);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(100);

        builder.Property(x => x.PreferredLanguage)
            .HasMaxLength(10)
            .HasDefaultValue("en")
            .IsRequired();

        builder.Property(x => x.CountryCode)
            .HasMaxLength(20);

        builder.Property(x => x.TimeZone)
            .HasMaxLength(200);

        builder.Property(x => x.ProfilePhotoUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.DateOfBirth)
            .HasColumnType("date");

        builder.Property(x => x.Occupation)
            .HasMaxLength(400);

        builder.Property(x => x.Company)
            .HasMaxLength(400);

        builder.Property(x => x.Biography)
            .HasMaxLength(4000);

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
            .HasForeignKey<PersonProfile>(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PersonProfile_Person");

        builder.HasOne(x => x.PreferredLanguageData)
            .WithMany(x => x.PersonProfiles)
            .HasForeignKey(x => x.PreferredLanguage)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_PersonProfile_PreferredLanguage");

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_PersonProfile_PersonId");
    }
}
