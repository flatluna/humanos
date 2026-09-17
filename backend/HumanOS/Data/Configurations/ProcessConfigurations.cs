using HumanOS.Models.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ProcessDocumentRecordConfiguration : IEntityTypeConfiguration<ProcessDocumentRecord>
{
    public void Configure(EntityTypeBuilder<ProcessDocumentRecord> builder)
    {
        builder.ToTable("ProcessDocument", "dbo");
        builder.HasKey(x => x.ProcessDocumentId);

        builder.Property(x => x.Title).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Version).HasMaxLength(20).HasDefaultValue("1.0");
        builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("Draft");
        builder.Property(x => x.StorageContainer).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StorageBlobPath).HasMaxLength(500).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.JobRole)
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Chapters)
            .WithOne(c => c.ProcessDocument)
            .HasForeignKey(c => c.ProcessDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Steps)
            .WithOne(s => s.ProcessDocument)
            .HasForeignKey(s => s.ProcessDocumentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProcessChapterRecordConfiguration : IEntityTypeConfiguration<ProcessChapterRecord>
{
    public void Configure(EntityTypeBuilder<ProcessChapterRecord> builder)
    {
        builder.ToTable("ProcessChapter", "dbo");
        builder.HasKey(x => x.ProcessChapterId);

        builder.Property(x => x.Title).HasMaxLength(250).IsRequired();

        builder.HasMany(x => x.Steps)
            .WithOne(s => s.ProcessChapter)
            .HasForeignKey(s => s.ProcessChapterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProcessStepRecordConfiguration : IEntityTypeConfiguration<ProcessStepRecord>
{
    public void Configure(EntityTypeBuilder<ProcessStepRecord> builder)
    {
        builder.ToTable("ProcessStep", "dbo");
        builder.HasKey(x => x.ProcessStepId);

        builder.Property(x => x.StepNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.SourcePagesJson).HasMaxLength(100);

        builder.HasMany(x => x.Skills)
            .WithOne(s => s.ProcessStep)
            .HasForeignKey(s => s.ProcessStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProcessStepSkillRecordConfiguration : IEntityTypeConfiguration<ProcessStepSkillRecord>
{
    public void Configure(EntityTypeBuilder<ProcessStepSkillRecord> builder)
    {
        builder.ToTable("ProcessStepSkill", "dbo");
        builder.HasKey(x => x.ProcessStepSkillId);

        builder.Property(x => x.SkillName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Criticality).HasMaxLength(20).HasDefaultValue("critical");
        builder.Property(x => x.CoverageStatus).HasMaxLength(50).HasDefaultValue("new_skill_required");
        builder.Property(x => x.MatchedCourseName).HasMaxLength(250);

        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
