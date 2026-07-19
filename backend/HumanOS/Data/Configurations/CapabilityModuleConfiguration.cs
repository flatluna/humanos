using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityModuleConfiguration
    : IEntityTypeConfiguration<CapabilityModule>
{
    public void Configure(EntityTypeBuilder<CapabilityModule> builder)
    {
        builder.ToTable("CapabilityModule", "dbo");

        builder.HasKey(x => x.CapabilityModuleId)
            .HasName("PK_CapabilityModule");

        builder.Property(x => x.CapabilityModuleId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityLevelId)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Script)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.MetricRationale)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.RecallRequirement)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.LearnerProduction)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.LearnerTask)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.CapabilityLevel)
            .WithMany(x => x.Modules)
            .HasForeignKey(x => x.CapabilityLevelId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityModule_CapabilityLevel");

        builder.HasIndex(x => x.CapabilityLevelId)
            .HasDatabaseName("IX_CapabilityModule_CapabilityLevelId");
    }
}
