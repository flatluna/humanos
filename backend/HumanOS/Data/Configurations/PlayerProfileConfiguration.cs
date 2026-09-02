using HumanOS.Models.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
{
    public void Configure(EntityTypeBuilder<PlayerProfile> builder)
    {
        builder.ToTable("PlayerProfile", "dbo");

        builder.HasKey(x => x.Id)
            .HasName("PK_PlayerProfile");

        builder.Property(x => x.Id)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.TotalPoints)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.CurrentLevel)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.CurrentLevelName)
            .HasMaxLength(50);

        builder.Property(x => x.CurrentStreakDays)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.LongestStreakDays)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.StreakFreezesAvailable)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.LastActivityDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.TotalEngramsMastered)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_PlayerProfile_PersonId");
    }
}
