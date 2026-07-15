using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityModuleMetricConfiguration
    : IEntityTypeConfiguration<CapabilityModuleMetric>
{
    public void Configure(
        EntityTypeBuilder<CapabilityModuleMetric> builder)
    {
        builder.ToTable("CapabilityModuleMetric", "dbo");

        builder.HasKey(x => new
        {
            x.CapabilityModuleId,
            x.Metric
        })
        .HasName("PK_CapabilityModuleMetric");

        builder.Property(x => x.CapabilityModuleId)
            .IsRequired();

        builder.Property(x => x.Metric)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(x => x.CapabilityModule)
            .WithMany(x => x.Metrics)
            .HasForeignKey(x => x.CapabilityModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityModuleMetric_CapabilityModule");
    }
}
