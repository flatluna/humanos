using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad CapabilityGraph.
/// </summary>
public class CapabilityGraphConfiguration : IEntityTypeConfiguration<CapabilityGraph>
{
    public void Configure(EntityTypeBuilder<CapabilityGraph> builder)
    {
        builder.HasKey(g => g.CapabilityGraphId);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(g => g.Description)
            .HasMaxLength(2000);

        builder.Property(g => g.ExecutiveSummary)
            .HasColumnType("nvarchar(max)");

        builder.Property(g => g.KeyEntitiesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(g => g.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a Capability — relación 1:1
        builder.HasOne(g => g.Capability)
            .WithOne(c => c.CapabilityGraph)
            .HasForeignKey<CapabilityGraph>(g => g.CapabilityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Index para búsquedas por CapabilityId
        builder.HasIndex(g => g.CapabilityId)
            .IsUnique();
    }
}
