using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityModuleChapterConfiguration
    : IEntityTypeConfiguration<CapabilityModuleChapter>
{
    public void Configure(EntityTypeBuilder<CapabilityModuleChapter> builder)
    {
        builder.ToTable("CapabilityModuleChapter", "dbo");

        builder.HasKey(x => x.CapabilityModuleChapterId)
            .HasName("PK_CapabilityModuleChapter");

        builder.Property(x => x.CapabilityModuleChapterId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityModuleId)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.TeachingContent)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.IsPrimaryWeight)
            .IsRequired();

        builder.Property(x => x.RecallPrompt)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.IsCumulativeRecall)
            .IsRequired();

        builder.Property(x => x.PredictionPrompt)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.MiniPracticePrompt)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.CapabilityModule)
            .WithMany(x => x.Chapters)
            .HasForeignKey(x => x.CapabilityModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_CapabilityModuleChapter_CapabilityModule");

        builder.HasIndex(x => x.CapabilityModuleId)
            .HasDatabaseName("IX_CapabilityModuleChapter_CapabilityModuleId");
    }
}
