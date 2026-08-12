using HumanOS.Models.Capabilities.Traditional;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public class CapabilityTraditionalIllustrationConfiguration : IEntityTypeConfiguration<CapabilityTraditionalIllustration>
{
    public void Configure(EntityTypeBuilder<CapabilityTraditionalIllustration> builder)
    {
        builder.HasKey(i => i.CapabilityTraditionalIllustrationId);

        builder.Property(i => i.StoragePath).IsRequired().HasMaxLength(1000);
        builder.Property(i => i.Prompt).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(i => i.Caption).HasMaxLength(2000);
        builder.Property(i => i.Purpose).IsRequired();
        builder.Property(i => i.ImageModel).IsRequired().HasMaxLength(200);
        builder.Property(i => i.CreatedDate).HasDefaultValueSql("GETUTCDATE()").IsRequired();

        builder.HasIndex(i => i.CapabilityId);
    }
}

public class CapabilityChapterConfiguration : IEntityTypeConfiguration<CapabilityChapter>
{
    public void Configure(EntityTypeBuilder<CapabilityChapter> builder)
    {
        builder.HasKey(c => c.CapabilityChapterId);

        builder.Property(c => c.Title).IsRequired().HasMaxLength(500);
        builder.Property(c => c.CreatedDate).HasDefaultValueSql("GETUTCDATE()").IsRequired();

        builder.HasOne(c => c.Capability)
            .WithMany(cap => cap.TraditionalChapters)
            .HasForeignKey(c => c.CapabilityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.CapabilityId);
    }
}

public class CapabilityLessonConfiguration : IEntityTypeConfiguration<CapabilityLesson>
{
    public void Configure(EntityTypeBuilder<CapabilityLesson> builder)
    {
        builder.HasKey(l => l.CapabilityLessonId);

        builder.Property(l => l.Title).IsRequired().HasMaxLength(500);
        builder.Property(l => l.CreatedDate).HasDefaultValueSql("GETUTCDATE()").IsRequired();

        builder.HasOne(l => l.CapabilityChapter)
            .WithMany(c => c.Lessons)
            .HasForeignKey(l => l.CapabilityChapterId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.CapabilityChapterId);
    }
}

public class CapabilityLessonCardConfiguration : IEntityTypeConfiguration<CapabilityLessonCard>
{
    public void Configure(EntityTypeBuilder<CapabilityLessonCard> builder)
    {
        builder.HasKey(c => c.CapabilityLessonCardId);

        builder.Property(c => c.Title).IsRequired().HasMaxLength(500);
        builder.Property(c => c.Text).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(c => c.CreatedDate).HasDefaultValueSql("GETUTCDATE()").IsRequired();

        builder.HasOne(c => c.CapabilityLesson)
            .WithMany(l => l.Cards)
            .HasForeignKey(c => c.CapabilityLessonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Illustration)
            .WithMany()
            .HasForeignKey(c => c.CapabilityTraditionalIllustrationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.CapabilityLessonId);
    }
}

public class CapabilityChapterExamQuestionConfiguration : IEntityTypeConfiguration<CapabilityChapterExamQuestion>
{
    public void Configure(EntityTypeBuilder<CapabilityChapterExamQuestion> builder)
    {
        builder.HasKey(q => q.CapabilityChapterExamQuestionId);

        builder.Property(q => q.QuestionText).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(q => q.OptionsJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(q => q.CreatedDate).HasDefaultValueSql("GETUTCDATE()").IsRequired();

        builder.HasOne(q => q.CapabilityChapter)
            .WithMany(c => c.ExamQuestions)
            .HasForeignKey(q => q.CapabilityChapterId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.Illustration)
            .WithMany()
            .HasForeignKey(q => q.CapabilityTraditionalIllustrationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(q => q.CapabilityChapterId);
    }
}
