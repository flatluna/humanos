using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad LearningEvidence.
/// </summary>
public class LearningEvidenceConfiguration : IEntityTypeConfiguration<LearningEvidence>
{
    public void Configure(EntityTypeBuilder<LearningEvidence> builder)
    {
        builder.HasKey(e => e.LearningEvidenceId);

        builder.Property(e => e.StudentResponse)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.TutorPrompt)
            .IsRequired(false)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.TutorScore)
            .IsRequired(false);

        builder.Property(e => e.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a LearningSessionStep — ownership real, APPEND-ONLY (sin índice único).
        builder.HasOne(e => e.LearningSessionStep)
            .WithMany(s => s.Evidence)
            .HasForeignKey(e => e.LearningSessionStepId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.LearningSessionStepId);
    }
}
