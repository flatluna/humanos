using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityGraphNodeKnowledgeChunkConfiguration
    : IEntityTypeConfiguration<CapabilityGraphNodeKnowledgeChunk>
{
    public void Configure(
        EntityTypeBuilder<CapabilityGraphNodeKnowledgeChunk> builder)
    {
        builder.ToTable("CapabilityGraphNodeKnowledgeChunk", "dbo");

        builder.HasKey(x => x.CapabilityGraphNodeKnowledgeChunkId)
            .HasName("PK_CapabilityGraphNodeKnowledgeChunk");

        builder.Property(x => x.CapabilityGraphNodeKnowledgeChunkId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityGraphNodeId)
            .IsRequired();

        builder.Property(x => x.CapabilityGraphId)
            .IsRequired();

        builder.Property(x => x.SourceField)
            .HasMaxLength(64)
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

        // Real FK only to CapabilityGraphNode (which already cascades from
        // CapabilityGraph) — CapabilityGraphId above is a deliberately
        // UNCONSTRAINED denormalized column (index only, no FK) so this
        // doesn't create a second cascade path from CapabilityGraph, the
        // same "multiple cascade paths" SQL Server error V1's
        // CapabilityKnowledgeChunkConfiguration avoids via Restrict.
        builder.HasOne(x => x.CapabilityGraphNode)
            .WithMany(x => x.KnowledgeChunks)
            .HasForeignKey(x => x.CapabilityGraphNodeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphNode");

        builder.HasIndex(x => x.CapabilityGraphNodeId)
            .HasDatabaseName("IX_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphNodeId");

        builder.HasIndex(x => x.CapabilityGraphId)
            .HasDatabaseName("IX_CapabilityGraphNodeKnowledgeChunk_CapabilityGraphId");
    }
}
