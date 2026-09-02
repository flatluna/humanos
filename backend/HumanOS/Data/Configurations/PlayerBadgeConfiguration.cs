using HumanOS.Models.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PlayerBadgeConfiguration : IEntityTypeConfiguration<PlayerBadge>
{
    public void Configure(EntityTypeBuilder<PlayerBadge> builder)
    {
        builder.ToTable("PlayerBadge", "dbo");

        builder.HasKey(x => x.Id)
            .HasName("PK_PlayerBadge");

        builder.Property(x => x.Id)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.BadgeCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EarnedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(x => new { x.PersonId, x.BadgeCode })
            .IsUnique()
            .HasDatabaseName("UX_PlayerBadge_Person_BadgeCode");

        builder.HasIndex(x => x.PersonId)
            .HasDatabaseName("IX_PlayerBadge_PersonId");
    }
}
