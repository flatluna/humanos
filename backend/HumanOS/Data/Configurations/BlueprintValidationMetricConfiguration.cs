using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad BlueprintValidationMetric.
/// </summary>
public class BlueprintValidationMetricConfiguration : IEntityTypeConfiguration<BlueprintValidationMetric>
{
    public void Configure(EntityTypeBuilder<BlueprintValidationMetric> builder)
    {
        builder.HasKey(m => m.BlueprintValidationMetricId);

        builder.Property(m => m.MetricName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.MetricValue)
            .IsRequired();

        // FK a BlueprintValidation — relación 1:N
        builder.HasOne(m => m.BlueprintValidation)
            .WithMany(v => v.Metrics)
            .HasForeignKey(m => m.BlueprintValidationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.BlueprintValidationId);
    }
}
