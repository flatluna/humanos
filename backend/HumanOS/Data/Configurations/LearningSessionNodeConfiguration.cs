using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad LearningSessionNode.
/// </summary>
public class LearningSessionNodeConfiguration : IEntityTypeConfiguration<LearningSessionNode>
{
    public void Configure(EntityTypeBuilder<LearningSessionNode> builder)
    {
        builder.HasKey(n => n.LearningSessionNodeId);

        builder.Property(n => n.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(n => n.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a LearningSession — ownership real: si se borra la sesión, se borra su progreso por nodo.
        builder.HasOne(n => n.LearningSession)
            .WithMany(s => s.Nodes)
            .HasForeignKey(n => n.LearningSessionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // FK a CapabilityGraphNode — solo referencia, nunca se cascadea.
        builder.HasOne(n => n.CapabilityGraphNode)
            .WithMany()
            .HasForeignKey(n => n.CapabilityGraphNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // FK a NodeExperienceBlueprint — solo referencia, nunca se cascadea.
        builder.HasOne(n => n.NodeExperienceBlueprint)
            .WithMany()
            .HasForeignKey(n => n.NodeExperienceBlueprintId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.LearningSessionId);
        builder.HasIndex(n => n.CapabilityGraphNodeId);
        builder.HasIndex(n => n.NodeExperienceBlueprintId);
    }
}
