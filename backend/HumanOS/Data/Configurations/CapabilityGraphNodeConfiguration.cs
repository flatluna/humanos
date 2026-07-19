using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad CapabilityGraphNode.
/// </summary>
public class CapabilityGraphNodeConfiguration : IEntityTypeConfiguration<CapabilityGraphNode>
{
    public void Configure(EntityTypeBuilder<CapabilityGraphNode> builder)
    {
        builder.HasKey(n => n.CapabilityGraphNodeId);

        builder.Property(n => n.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Description)
            .HasMaxLength(2000);

        builder.Property(n => n.NodeType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(n => n.SortOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(n => n.AcademicDefinition)
            .HasColumnType("nvarchar(max)");

        builder.Property(n => n.Interpretation)
            .HasColumnType("nvarchar(max)");

        builder.Property(n => n.ExamplesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(n => n.ApplicationsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(n => n.ReferencesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(n => n.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a CapabilityGraph — relación 1:N
        builder.HasOne(n => n.CapabilityGraph)
            .WithMany(g => g.Nodes)
            .HasForeignKey(n => n.CapabilityGraphId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Index para búsquedas rápidas por CapabilityGraphId y Name
        builder.HasIndex(n => new { n.CapabilityGraphId, n.Name })
            .IsUnique();

        // Index para ordenamiento
        builder.HasIndex(n => new { n.CapabilityGraphId, n.SortOrder });
    }
}
