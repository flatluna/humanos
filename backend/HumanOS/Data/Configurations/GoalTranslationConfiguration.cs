using HumanOS.Models.Goals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class GoalTranslationConfiguration
    : IEntityTypeConfiguration<GoalTranslation>
{
    public void Configure(
        EntityTypeBuilder<GoalTranslation> builder)
    {
        builder.ToTable("GoalTranslation", "dbo");

        builder.HasKey(x => new
        {
            x.GoalId,
            x.LanguageCode
        })
        .HasName("PK_GoalTranslation");

        builder.Property(x => x.GoalId)
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

        builder.HasOne(x => x.Goal)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.GoalId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_GoalTranslation_Goal");

        builder.HasOne(x => x.Language)
            .WithMany(x => x.GoalTranslations)
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_GoalTranslation_Language");
    }
}
