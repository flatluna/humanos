using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityTranslationConfiguration
    : IEntityTypeConfiguration<CapabilityTranslation>
{
    public void Configure(
        EntityTypeBuilder<CapabilityTranslation> builder)
    {
        builder.ToTable("CapabilityTranslation", "dbo");

        builder.HasKey(x => new
        {
            x.CapabilityId,
            x.LanguageCode
        })
        .HasName("PK_CapabilityTranslation");

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.LanguageCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Capability)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityTranslation_Capability");

        builder.HasOne(x => x.Language)
            .WithMany(x => x.CapabilityTranslations)
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_CapabilityTranslation_Language");
    }
}
