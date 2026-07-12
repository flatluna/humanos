using HumanOS.Models.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class AssessmentAttemptConfiguration
    : IEntityTypeConfiguration<AssessmentAttempt>
{
    public void Configure(
        EntityTypeBuilder<AssessmentAttempt> builder)
    {
        builder.ToTable("AssessmentAttempt", "dbo");

        builder.HasKey(x => x.AssessmentAttemptId)
            .HasName("PK_AssessmentAttempt");

        builder.Property(x => x.AssessmentAttemptId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.AssessmentId)
            .IsRequired();

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.Score)
            .HasPrecision(5, 2);

        builder.Property(x => x.AssistanceLevel)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.StartedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.CompletedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Assessment)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_AssessmentAttempt_Assessment");

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_AssessmentAttempt_Person");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_AssessmentAttempt_Score",
                """
                [Score] IS NULL
                OR [Score] BETWEEN 0 AND 100
                """);

            table.HasCheckConstraint(
                "CK_AssessmentAttempt_AssistanceLevel",
                "[AssistanceLevel] BETWEEN 0 AND 5");
        });
    }
}
