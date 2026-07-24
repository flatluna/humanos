using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonStartingPointConfiguration
    : IEntityTypeConfiguration<PersonStartingPoint>
{
    public void Configure(EntityTypeBuilder<PersonStartingPoint> builder)
    {
        builder.ToTable("PersonStartingPoint", "dbo");

        builder.HasKey(x => x.PersonStartingPointId)
            .HasName("PK_PersonStartingPoint");

        builder.Property(x => x.PersonStartingPointId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.SelectedCapabilityIds)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.GapCapabilitiesBySubjectJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.AcceptedRecommendationsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.Completed)
            .HasDefaultValue(false)
            .IsRequired();

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
            .HasForeignKey<PersonStartingPoint>(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PersonStartingPoint_Person");

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_PersonStartingPoint_PersonId");
    }
}
