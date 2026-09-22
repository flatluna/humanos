using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonCqiProfileConfiguration : IEntityTypeConfiguration<PersonCqiProfile>
{
    public void Configure(EntityTypeBuilder<PersonCqiProfile> builder)
    {
        builder.ToTable("PersonCqiProfile", "dbo");

        builder.HasKey(x => x.PersonCqiProfileId)
            .HasName("PK_PersonCqiProfile");

        builder.Property(x => x.PersonCqiProfileId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.LeadingPatternType)
            .HasMaxLength(100);

        builder.Property(x => x.LeadingPatternName)
            .HasMaxLength(200);

        builder.Property(x => x.ProfileJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}")
            .IsRequired();

        builder.Property(x => x.SourceFileName)
            .HasMaxLength(500);

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
            .HasConstraintName("FK_PersonCqiProfile_Person")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PersonId)
            .HasDatabaseName("UX_PersonCqiProfile_PersonId")
            .IsUnique();
    }
}
