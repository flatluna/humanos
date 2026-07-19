using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad CapabilityGraphEdge.
/// </summary>
public class CapabilityGraphEdgeConfiguration : IEntityTypeConfiguration<CapabilityGraphEdge>
{
    public void Configure(EntityTypeBuilder<CapabilityGraphEdge> builder)
    {
        builder.HasKey(e => e.CapabilityGraphEdgeId);

        builder.Property(e => e.RelationshipType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a CapabilityGraph — relación 1:N
        builder.HasOne(e => e.CapabilityGraph)
            .WithMany(g => g.Edges)
            .HasForeignKey(e => e.CapabilityGraphId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // FK a SourceNode
        builder.HasOne(e => e.SourceNode)
            .WithMany(n => n.OutgoingEdges)
            .HasForeignKey(e => e.SourceNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        // FK a TargetNode
        builder.HasOne(e => e.TargetNode)
            .WithMany(n => n.IncomingEdges)
            .HasForeignKey(e => e.TargetNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        // Index para búsquedas por CapabilityGraphId
        builder.HasIndex(e => e.CapabilityGraphId);

        // Index para búsquedas de relaciones (SourceNode -> TargetNode)
        builder.HasIndex(e => new { e.SourceNodeId, e.TargetNodeId });

        // Constraint: SourceNode y TargetNode deben estar en el mismo CapabilityGraph
        // (esto se valida en la aplicación, pero es bueno tenerlo documentado)
    }
}
