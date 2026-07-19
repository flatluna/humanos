using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad NodeExperienceBlueprint.
/// </summary>
public class NodeExperienceBlueprintConfiguration : IEntityTypeConfiguration<NodeExperienceBlueprint>
{
    public void Configure(EntityTypeBuilder<NodeExperienceBlueprint> builder)
    {
        builder.HasKey(b => b.NodeExperienceBlueprintId);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.Description)
            .HasMaxLength(2000);

        builder.Property(b => b.Version)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(b => b.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a CapabilityGraphNode — relación 1:N (un nodo puede tener varios blueprints)
        builder.HasOne(b => b.CapabilityGraphNode)
            .WithMany(n => n.ExperienceBlueprints)
            .HasForeignKey(b => b.CapabilityGraphNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Index para búsquedas rápidas por CapabilityGraphNodeId
        builder.HasIndex(b => b.CapabilityGraphNodeId);

        // Un nodo no puede tener dos blueprints con el mismo Name+Version
        builder.HasIndex(b => new { b.CapabilityGraphNodeId, b.Name, b.Version })
            .IsUnique();
    }
}
