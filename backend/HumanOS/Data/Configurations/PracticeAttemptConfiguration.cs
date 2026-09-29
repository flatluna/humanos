using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PracticeAttemptConfiguration : IEntityTypeConfiguration<PracticeAttempt>
{
    public void Configure(EntityTypeBuilder<PracticeAttempt> builder)
    {
        builder.ToTable("PracticeAttempt", "dbo");

        builder.HasKey(x => x.PracticeAttemptId)
            .HasName("PK_PracticeAttempt");

        builder.Property(x => x.PracticeAttemptId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PracticeSessionId)
            .IsRequired();

        builder.Property(x => x.ExerciseIndex)
            .IsRequired();

        builder.Property(x => x.StudentAnswer)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Score)
            .IsRequired();

        builder.Property(x => x.Feedback)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.PointsAwarded)
            .IsRequired();

        builder.Property(x => x.CompletedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne<PracticeSession>()
            .WithMany()
            .HasForeignKey(x => x.PracticeSessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PracticeAttempt_PracticeSession");

        builder.HasIndex(x => new { x.PracticeSessionId, x.ExerciseIndex })
            .IsUnique()
            .HasDatabaseName("IX_PracticeAttempt_PracticeSessionId_ExerciseIndex");
    }
}
