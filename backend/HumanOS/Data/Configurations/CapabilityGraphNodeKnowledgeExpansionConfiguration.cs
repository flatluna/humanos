using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad CapabilityGraphNodeKnowledgeExpansion.
/// </summary>
public class CapabilityGraphNodeKnowledgeExpansionConfiguration : IEntityTypeConfiguration<CapabilityGraphNodeKnowledgeExpansion>
{
    public void Configure(EntityTypeBuilder<CapabilityGraphNodeKnowledgeExpansion> builder)
    {
        builder.HasKey(e => e.CapabilityGraphNodeKnowledgeExpansionId);

        builder.Property(e => e.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a CapabilityGraphNode — una fila por nodo (cache compartida
        // entre todos los alumnos), por eso el índice único.
        builder.HasOne(e => e.CapabilityGraphNode)
            .WithMany()
            .HasForeignKey(e => e.CapabilityGraphNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CapabilityGraphNodeId)
            .IsUnique();

        // FK opcional al diagrama — sin cascada (el diagrama vive en su
        // propia tabla/ciclo de vida; borrar el nodo ya cascadea por la FK
        // de arriba de todas formas).
        builder.HasOne(e => e.DiagramIllustration)
            .WithMany()
            .HasForeignKey(e => e.DiagramIllustrationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
