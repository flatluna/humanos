using HumanOS.Models.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class BadgeCatalogConfiguration : IEntityTypeConfiguration<BadgeCatalog>
{
    public void Configure(EntityTypeBuilder<BadgeCatalog> builder)
    {
        builder.ToTable("BadgeCatalog", "dbo");

        builder.HasKey(x => x.Id)
            .HasName("PK_BadgeCatalog");

        builder.Property(x => x.Id)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(300);

        builder.Property(x => x.IconEmoji)
            .HasMaxLength(20);

        builder.Property(x => x.RuleType)
            .HasMaxLength(50);

        builder.Property(x => x.RuleThreshold)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_BadgeCatalog_Code");
    }
}
