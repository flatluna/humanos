using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class HumanProfileConfiguration
    : IEntityTypeConfiguration<HumanProfile>
{
    public void Configure(EntityTypeBuilder<HumanProfile> builder)
    {
        builder.ToTable("HumanProfile", "dbo");

        builder.HasKey(x => x.HumanProfileId)
            .HasName("PK_HumanProfile");

        builder.Property(x => x.HumanProfileId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.MissionStatement)
            .HasMaxLength(4000);

        builder.Property(x => x.PrimaryGoal)
            .HasMaxLength(2000);

        builder.Property(x => x.LearningStyle)
            .HasMaxLength(200);

        builder.Property(x => x.CurrentLifeStage)
            .HasMaxLength(200);

        builder.Property(x => x.WeeklyAvailabilityHours)
            .HasPrecision(5, 2);

        builder.Property(x => x.MotivationScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.ConfidenceScore)
            .HasPrecision(5, 2);

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
            .HasForeignKey<HumanProfile>(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_HumanProfile_Person");

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_HumanProfile_PersonId");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_HumanProfile_Scores",
                """
                ([MotivationScore] IS NULL
                    OR [MotivationScore] BETWEEN 0 AND 100)
                AND
                ([ConfidenceScore] IS NULL
                    OR [ConfidenceScore] BETWEEN 0 AND 100)
                """);

            table.HasCheckConstraint(
                "CK_HumanProfile_WeeklyAvailabilityHours",
                """
                [WeeklyAvailabilityHours] IS NULL
                OR [WeeklyAvailabilityHours] BETWEEN 0 AND 168
                """);
        });
    }
}
