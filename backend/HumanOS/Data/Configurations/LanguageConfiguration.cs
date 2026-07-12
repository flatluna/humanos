using HumanOS.Models.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class LanguageConfiguration
    : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("Language", "dbo");

        builder.HasKey(x => x.LanguageCode)
            .HasName("PK_Language");

        builder.Property(x => x.LanguageCode)
            .HasMaxLength(10)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.EnglishName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.NativeName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();
    }
}
