using HumanOS.Models.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ProgramCapabilityConfiguration : IEntityTypeConfiguration<ProgramCapability>
{
    public void Configure(EntityTypeBuilder<ProgramCapability> builder)
    {
        builder.ToTable("ProgramCapability", "dbo");

        builder.HasKey(x => x.ProgramCapabilityId)
            .HasName("PK_ProgramCapability");

        builder.Property(x => x.ProgramCapabilityId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.IsRequired)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.PhaseLabel)
            .HasMaxLength(400);

        builder.Property(x => x.Objectives)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Requirements)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Program)
            .WithMany(x => x.ProgramCapabilities)
            .HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ProgramCapability_Program");

        // Restrict (not Cascade) — deleting a Capability that's referenced
        // by a Program must not silently delete Program membership rows;
        // same defensive FK style as CapabilityKnowledgeChunkConfiguration.
        builder.HasOne(x => x.Capability)
            .WithMany()
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProgramCapability_Capability");

        builder.HasIndex(x => new { x.ProgramId, x.CapabilityId })
            .IsUnique()
            .HasDatabaseName("UX_ProgramCapability_Program_Capability");

        builder.HasIndex(x => new { x.ProgramId, x.SortOrder })
            .IsUnique()
            .HasDatabaseName("UX_ProgramCapability_Program_SortOrder");
    }
}
