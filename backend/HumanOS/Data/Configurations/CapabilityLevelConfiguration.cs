using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityLevelConfiguration
    : IEntityTypeConfiguration<CapabilityLevel>
{
    public void Configure(EntityTypeBuilder<CapabilityLevel> builder)
    {
        builder.ToTable("CapabilityLevel", "dbo");

        builder.HasKey(x => x.CapabilityLevelId)
            .HasName("PK_CapabilityLevel");

        builder.Property(x => x.CapabilityLevelId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.Layer)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.HumanTransformation)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Capability)
            .WithMany(x => x.Levels)
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityLevel_Capability");

        builder.HasIndex(x => x.CapabilityId)
            .HasDatabaseName("IX_CapabilityLevel_CapabilityId");
    }
}
