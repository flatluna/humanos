using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad CapabilityGenerationUsage —
/// ver el doc comment de la entidad para el propósito (dashboard de costos).
/// </summary>
public class CapabilityGenerationUsageConfiguration : IEntityTypeConfiguration<CapabilityGenerationUsage>
{
    public void Configure(EntityTypeBuilder<CapabilityGenerationUsage> builder)
    {
        builder.HasKey(u => u.CapabilityGenerationUsageId);

        builder.Property(u => u.AgentName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.ModelName)
            .HasMaxLength(100);

        builder.Property(u => u.SectionLabel)
            .HasMaxLength(500);

        builder.Property(u => u.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasOne(u => u.Capability)
            .WithMany()
            .HasForeignKey(u => u.CapabilityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => u.CapabilityId);

        builder.HasIndex(u => u.CapabilityGraphId);
    }
}
