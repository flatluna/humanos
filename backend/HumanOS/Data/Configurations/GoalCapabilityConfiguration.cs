using HumanOS.Models.Goals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class GoalCapabilityConfiguration
    : IEntityTypeConfiguration<GoalCapability>
{
    public void Configure(
        EntityTypeBuilder<GoalCapability> builder)
    {
        builder.ToTable("GoalCapability", "dbo");

        builder.HasKey(x => new
        {
            x.GoalId,
            x.CapabilityId
        })
        .HasName("PK_GoalCapability");

        builder.Property(x => x.GoalId)
            .IsRequired();

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.IsRequired)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Goal)
            .WithMany(x => x.GoalCapabilities)
            .HasForeignKey(x => x.GoalId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_GoalCapability_Goal");

        builder.HasOne(x => x.Capability)
            .WithMany()
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_GoalCapability_Capability");
    }
}
