using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad LearningSessionStep.
/// </summary>
public class LearningSessionStepConfiguration : IEntityTypeConfiguration<LearningSessionStep>
{
    public void Configure(EntityTypeBuilder<LearningSessionStep> builder)
    {
        builder.HasKey(s => s.LearningSessionStepId);

        builder.Property(s => s.StepType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a LearningSessionNode — ownership real.
        builder.HasOne(s => s.LearningSessionNode)
            .WithMany(n => n.Steps)
            .HasForeignKey(s => s.LearningSessionNodeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Un nodo solo puede tener un step activo de cada StepType a la vez.
        builder.HasIndex(s => new { s.LearningSessionNodeId, s.StepType })
            .IsUnique();
    }
}
