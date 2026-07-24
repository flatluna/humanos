using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonFutureDirectionConfiguration
    : IEntityTypeConfiguration<PersonFutureDirection>
{
    public void Configure(EntityTypeBuilder<PersonFutureDirection> builder)
    {
        builder.ToTable("PersonFutureDirection", "dbo");

        builder.HasKey(x => x.PersonFutureDirectionId)
            .HasName("PK_PersonFutureDirection");

        builder.Property(x => x.PersonFutureDirectionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.SelectedGoalIds)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.SelectedMotivationCodes)
            .HasColumnType("nvarchar(max)")
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
            .HasForeignKey<PersonFutureDirection>(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PersonFutureDirection_Person");

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_PersonFutureDirection_PersonId");
    }
}
