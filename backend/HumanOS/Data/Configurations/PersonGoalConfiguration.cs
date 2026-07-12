using HumanOS.Models.Goals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonGoalConfiguration
    : IEntityTypeConfiguration<PersonGoal>
{
    public void Configure(EntityTypeBuilder<PersonGoal> builder)
    {
        builder.ToTable("PersonGoal", "dbo");

        builder.HasKey(x => x.PersonGoalId)
            .HasName("PK_PersonGoal");

        builder.Property(x => x.PersonGoalId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.GoalId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(100)
            .HasDefaultValue("Active")
            .IsRequired();

        builder.Property(x => x.ProgressPercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.TargetDate)
            .HasColumnType("date");

        builder.Property(x => x.StartedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.CompletedDate)
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
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PersonGoal_Person");

        builder.HasOne(x => x.Goal)
            .WithMany(x => x.PersonGoals)
            .HasForeignKey(x => x.GoalId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PersonGoal_Goal");

        builder.HasIndex(x => new
        {
            x.PersonId,
            x.GoalId
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_PersonGoal_PersonId_GoalId");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_PersonGoal_ProgressPercentage",
                "[ProgressPercentage] BETWEEN 0 AND 100");

            table.HasCheckConstraint(
                "CK_PersonGoal_Status",
                """
                [Status] IN
                (
                    'Active',
                    'Paused',
                    'Completed',
                    'Abandoned'
                )
                """);
        });
    }
}
