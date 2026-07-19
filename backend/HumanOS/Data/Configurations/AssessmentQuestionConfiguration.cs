using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad AssessmentQuestion.
/// </summary>
public class AssessmentQuestionConfiguration : IEntityTypeConfiguration<AssessmentQuestion>
{
    public void Configure(EntityTypeBuilder<AssessmentQuestion> builder)
    {
        builder.HasKey(q => q.AssessmentQuestionId);

        builder.Property(q => q.QuestionType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(q => q.Correctness)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(q => q.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a AssessmentRound — ownership real.
        builder.HasOne(q => q.AssessmentRound)
            .WithMany(r => r.Questions)
            .HasForeignKey(q => q.AssessmentRoundId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Una ronda no puede tener dos preguntas con el mismo índice.
        builder.HasIndex(q => new { q.AssessmentRoundId, q.QuestionIndex })
            .IsUnique();
    }
}
