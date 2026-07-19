using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class RuntimeWorkflowCheckpointConfiguration
    : IEntityTypeConfiguration<RuntimeWorkflowCheckpoint>
{
    public void Configure(EntityTypeBuilder<RuntimeWorkflowCheckpoint> builder)
    {
        builder.ToTable("RuntimeWorkflowCheckpoint", "dbo");

        builder.HasKey(x => x.RuntimeWorkflowCheckpointId)
            .HasName("PK_RuntimeWorkflowCheckpoint");

        builder.Property(x => x.RuntimeWorkflowCheckpointId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.SessionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CheckpointId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ParentCheckpointId)
            .HasMaxLength(100);

        builder.Property(x => x.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.SessionId, x.CheckpointId })
            .IsUnique()
            .HasDatabaseName("UX_RuntimeWorkflowCheckpoint_SessionId_CheckpointId");
    }
}
