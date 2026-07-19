using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad LearningAssessmentResult.
/// </summary>
public class LearningAssessmentResultConfiguration : IEntityTypeConfiguration<LearningAssessmentResult>
{
    public void Configure(EntityTypeBuilder<LearningAssessmentResult> builder)
    {
        builder.HasKey(r => r.LearningAssessmentResultId);

        builder.Property(r => r.Score)
            .IsRequired();

        builder.Property(r => r.Passed)
            .IsRequired();

        builder.Property(r => r.Feedback)
            .HasColumnType("nvarchar(max)");

        builder.Property(r => r.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a LearningSessionNode — ownership real, APPEND-ONLY (permite reintentos).
        builder.HasOne(r => r.LearningSessionNode)
            .WithMany(n => n.AssessmentResults)
            .HasForeignKey(r => r.LearningSessionNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.LearningSessionNodeId);
    }
}
