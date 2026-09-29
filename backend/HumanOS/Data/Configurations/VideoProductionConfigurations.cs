using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>EF configuration for <see cref="VideoProduction"/> and
/// <see cref="VideoProductionClip"/> — see
/// HumanOS/Models/Capabilities/Studio/VideoProduction.cs.</summary>
public sealed class VideoProductionConfiguration : IEntityTypeConfiguration<VideoProduction>
{
    public void Configure(EntityTypeBuilder<VideoProduction> builder)
    {
        builder.ToTable("VideoProduction", "dbo");

        builder.HasKey(x => x.VideoProductionId)
            .HasName("PK_VideoProduction");

        builder.Property(x => x.VideoProductionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.FinalVideoStoragePath)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_VideoProduction_TenantId");
    }
}

public sealed class VideoProductionClipConfiguration : IEntityTypeConfiguration<VideoProductionClip>
{
    public void Configure(EntityTypeBuilder<VideoProductionClip> builder)
    {
        builder.ToTable("VideoProductionClip", "dbo");

        builder.HasKey(x => x.VideoProductionClipId)
            .HasName("PK_VideoProductionClip");

        builder.Property(x => x.VideoProductionClipId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Prompt)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.StoragePath).HasMaxLength(1000);
        builder.Property(x => x.VideoModel).HasMaxLength(200);
        builder.Property(x => x.ErrorMessage).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.VideoProductionId, x.SortOrder })
            .HasDatabaseName("IX_VideoProductionClip_VideoProductionId_SortOrder");
    }
}
