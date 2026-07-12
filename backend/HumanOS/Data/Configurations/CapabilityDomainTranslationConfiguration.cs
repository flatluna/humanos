using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityDomainTranslationConfiguration
    : IEntityTypeConfiguration<CapabilityDomainTranslation>
{
    public void Configure(
        EntityTypeBuilder<CapabilityDomainTranslation> builder)
    {
        builder.ToTable("CapabilityDomainTranslation", "dbo");

        builder.HasKey(x => new
        {
            x.CapabilityDomainId,
            x.LanguageCode
        })
        .HasName("PK_CapabilityDomainTranslation");

        builder.Property(x => x.CapabilityDomainId)
            .IsRequired();

        builder.Property(x => x.LanguageCode)
            .HasMaxLength(20)
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

        builder.HasOne(x => x.CapabilityDomain)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.CapabilityDomainId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityDomainTranslation_Domain");

        builder.HasOne(x => x.Language)
            .WithMany(x => x.CapabilityDomainTranslations)
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_CapabilityDomainTranslation_Language");
    }
}
