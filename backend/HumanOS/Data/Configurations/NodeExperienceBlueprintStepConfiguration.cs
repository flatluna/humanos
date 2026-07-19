using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad NodeExperienceBlueprintStep.
/// </summary>
public class NodeExperienceBlueprintStepConfiguration : IEntityTypeConfiguration<NodeExperienceBlueprintStep>
{
    public void Configure(EntityTypeBuilder<NodeExperienceBlueprintStep> builder)
    {
        builder.HasKey(s => s.NodeExperienceBlueprintStepId);

        builder.Property(s => s.StepType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(s => s.ReferencedIllustrationIdsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(s => s.SortOrder)
            .IsRequired();

        builder.Property(s => s.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a NodeExperienceBlueprint — relación 1:N
        builder.HasOne(s => s.NodeExperienceBlueprint)
            .WithMany(b => b.Steps)
            .HasForeignKey(s => s.NodeExperienceBlueprintId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Cada blueprint solo puede tener un step de cada StepType (el orden es fijo, sin duplicados)
        builder.HasIndex(s => new { s.NodeExperienceBlueprintId, s.StepType })
            .IsUnique();

        // Index para ordenamiento
        builder.HasIndex(s => new { s.NodeExperienceBlueprintId, s.SortOrder });
    }
}
