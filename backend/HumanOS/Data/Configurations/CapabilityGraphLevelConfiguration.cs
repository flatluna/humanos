using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad CapabilityGraphLevel.
/// </summary>
public class CapabilityGraphLevelConfiguration : IEntityTypeConfiguration<CapabilityGraphLevel>
{
    public void Configure(EntityTypeBuilder<CapabilityGraphLevel> builder)
    {
        builder.HasKey(l => l.CapabilityGraphLevelId);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a CapabilityGraph — relación 1:N
        builder.HasOne(l => l.CapabilityGraph)
            .WithMany(g => g.Levels)
            .HasForeignKey(l => l.CapabilityGraphId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Un único nombre por (grafo, índice de nivel)
        builder.HasIndex(l => new { l.CapabilityGraphId, l.LevelIndex })
            .IsUnique();
    }
}
