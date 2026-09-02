using HumanOS.Models.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class RewardLedgerEntryConfiguration : IEntityTypeConfiguration<RewardLedgerEntry>
{
    public void Configure(EntityTypeBuilder<RewardLedgerEntry> builder)
    {
        builder.ToTable("RewardLedgerEntry", "dbo");

        builder.HasKey(x => x.Id)
            .HasName("PK_RewardLedgerEntry");

        builder.Property(x => x.Id)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.EngramId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PointsBase)
            .IsRequired();

        builder.Property(x => x.DifficultyMultiplier)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.MemoryEffortMultiplier)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.SurpriseMultiplier)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.PointsAwarded)
            .IsRequired();

        builder.Property(x => x.WasSurprise)
            .IsRequired();

        builder.Property(x => x.SurpriseType)
            .HasMaxLength(20);

        builder.Property(x => x.ImpactScoreAtTime)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(300);

        builder.Property(x => x.SourceType)
            .HasMaxLength(30);

        builder.Property(x => x.EarnedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(x => new { x.PersonId, x.CourseId, x.EngramId })
            .IsUnique()
            .HasDatabaseName("UX_RewardLedgerEntry_Person_Course_Engram");

        builder.HasIndex(x => x.PersonId)
            .HasDatabaseName("IX_RewardLedgerEntry_PersonId");
    }
}
