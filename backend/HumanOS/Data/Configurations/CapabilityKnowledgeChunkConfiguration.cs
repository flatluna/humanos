using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityKnowledgeChunkConfiguration
    : IEntityTypeConfiguration<CapabilityKnowledgeChunk>
{
    public void Configure(
        EntityTypeBuilder<CapabilityKnowledgeChunk> builder)
    {
        builder.ToTable("CapabilityKnowledgeChunk", "dbo");

        builder.HasKey(x => x.CapabilityKnowledgeChunkId)
            .HasName("PK_CapabilityKnowledgeChunk");

        builder.Property(x => x.CapabilityKnowledgeChunkId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Embedding)
            .HasColumnType("vector(1536)")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        // Restrict (not Cascade) on both FKs: CapabilityModule already
        // cascades from Capability via CapabilityLevel, so a second
        // cascading path from Capability directly here would create a
        // multiple-cascade-paths error in SQL Server.
        builder.HasOne(x => x.Capability)
            .WithMany(x => x.KnowledgeChunks)
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_CapabilityKnowledgeChunk_Capability");

        builder.HasOne(x => x.CapabilityModule)
            .WithMany(x => x.KnowledgeChunks)
            .HasForeignKey(x => x.CapabilityModuleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_CapabilityKnowledgeChunk_CapabilityModule");

        builder.HasIndex(x => x.CapabilityId)
            .HasDatabaseName("IX_CapabilityKnowledgeChunk_CapabilityId");

        builder.HasIndex(x => x.CapabilityModuleId)
            .HasDatabaseName(
                "IX_CapabilityKnowledgeChunk_CapabilityModuleId");
    }
}
