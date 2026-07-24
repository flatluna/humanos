using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class SubjectTranslationConfiguration
    : IEntityTypeConfiguration<SubjectTranslation>
{
    public void Configure(
        EntityTypeBuilder<SubjectTranslation> builder)
    {
        builder.ToTable("SubjectTranslation", "dbo");

        builder.HasKey(x => new
        {
            x.SubjectId,
            x.LanguageCode
        })
        .HasName("PK_SubjectTranslation");

        builder.Property(x => x.SubjectId)
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

        builder.HasOne(x => x.Subject)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_SubjectTranslation_Subject");

        builder.HasOne(x => x.Language)
            .WithMany(x => x.SubjectTranslations)
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_SubjectTranslation_Language");
    }
}
