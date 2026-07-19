using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad CapabilityGraphNodeIllustration.
/// </summary>
public class CapabilityGraphNodeIllustrationConfiguration : IEntityTypeConfiguration<CapabilityGraphNodeIllustration>
{
    public void Configure(EntityTypeBuilder<CapabilityGraphNodeIllustration> builder)
    {
        builder.HasKey(i => i.CapabilityGraphNodeIllustrationId);

        builder.Property(i => i.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(i => i.Prompt)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(i => i.Caption)
            .HasMaxLength(2000);

        builder.Property(i => i.Purpose)
            .IsRequired();

        builder.Property(i => i.ImageModel)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Width)
            .IsRequired();

        builder.Property(i => i.Height)
            .IsRequired();

        builder.Property(i => i.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a CapabilityGraphNode — relación 1:N
        builder.HasOne(i => i.CapabilityGraphNode)
            .WithMany(n => n.Illustrations)
            .HasForeignKey(i => i.CapabilityGraphNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Index para búsquedas rápidas por CapabilityGraphNodeId
        builder.HasIndex(i => i.CapabilityGraphNodeId);
    }
}
