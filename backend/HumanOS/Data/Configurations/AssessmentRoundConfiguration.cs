using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad AssessmentRound.
/// </summary>
public class AssessmentRoundConfiguration : IEntityTypeConfiguration<AssessmentRound>
{
    public void Configure(EntityTypeBuilder<AssessmentRound> builder)
    {
        builder.HasKey(r => r.AssessmentRoundId);

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a LearningSessionNode — ownership real. Sin navigation
        // inversa en LearningSessionNode (se consulta directamente por
        // LearningSessionNodeId), igual que LearningAssessmentResult.
        builder.HasOne(r => r.LearningSessionNode)
            .WithMany()
            .HasForeignKey(r => r.LearningSessionNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Un nodo no puede tener dos rondas con el mismo número.
        builder.HasIndex(r => new { r.LearningSessionNodeId, r.RoundNumber })
            .IsUnique();
    }
}
