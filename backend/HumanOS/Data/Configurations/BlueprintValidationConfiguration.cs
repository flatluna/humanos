using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad BlueprintValidation.
/// </summary>
public class BlueprintValidationConfiguration : IEntityTypeConfiguration<BlueprintValidation>
{
    public void Configure(EntityTypeBuilder<BlueprintValidation> builder)
    {
        builder.HasKey(v => v.BlueprintValidationId);

        builder.Property(v => v.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.Score)
            .IsRequired();

        builder.Property(v => v.InputTokens)
            .IsRequired();

        builder.Property(v => v.OutputTokens)
            .IsRequired();

        builder.Property(v => v.TotalTokens)
            .IsRequired();

        builder.Property(v => v.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a NodeExperienceBlueprint — relación 1:N, APPEND-ONLY
        // (sin índice único: un blueprint puede validarse más de una vez).
        builder.HasOne(v => v.NodeExperienceBlueprint)
            .WithMany(b => b.Validations)
            .HasForeignKey(v => v.NodeExperienceBlueprintId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.NodeExperienceBlueprintId);
    }
}
