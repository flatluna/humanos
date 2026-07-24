using HumanOS.Models.Motivations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonMotivationConfiguration
    : IEntityTypeConfiguration<PersonMotivation>
{
    public void Configure(EntityTypeBuilder<PersonMotivation> builder)
    {
        builder.ToTable("PersonMotivation", "dbo");

        builder.HasKey(x => x.PersonMotivationId)
            .HasName("PK_PersonMotivation");

        builder.Property(x => x.PersonMotivationId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.MotivationId)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PersonMotivation_Person");

        builder.HasOne(x => x.Motivation)
            .WithMany(x => x.PersonMotivations)
            .HasForeignKey(x => x.MotivationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PersonMotivation_Motivation");

        builder.HasIndex(x => new
        {
            x.PersonId,
            x.MotivationId
        })
            .IsUnique()
            .HasDatabaseName("UX_PersonMotivation_PersonId_MotivationId");
    }
}
