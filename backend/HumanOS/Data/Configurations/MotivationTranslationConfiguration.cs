using HumanOS.Models.Motivations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class MotivationTranslationConfiguration
    : IEntityTypeConfiguration<MotivationTranslation>
{
    public void Configure(
        EntityTypeBuilder<MotivationTranslation> builder)
    {
        builder.ToTable("MotivationTranslation", "dbo");

        builder.HasKey(x => new
        {
            x.MotivationId,
            x.LanguageCode
        })
        .HasName("PK_MotivationTranslation");

        builder.Property(x => x.MotivationId)
            .IsRequired();

        builder.Property(x => x.LanguageCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Motivation)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.MotivationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_MotivationTranslation_Motivation");

        builder.HasOne(x => x.Language)
            .WithMany()
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_MotivationTranslation_Language");
    }
}
