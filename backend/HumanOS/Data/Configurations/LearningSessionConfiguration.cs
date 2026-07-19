using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad LearningSession.
/// </summary>
public class LearningSessionConfiguration : IEntityTypeConfiguration<LearningSession>
{
    public void Configure(EntityTypeBuilder<LearningSession> builder)
    {
        builder.HasKey(s => s.LearningSessionId);

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        // FK a Person — historial de aprendizaje se conserva aunque el Person
        // sea desactivado; nunca se cascadea el borrado.
        builder.HasOne(s => s.Person)
            .WithMany()
            .HasForeignKey(s => s.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // FK a Capability — misma razón: preservar historial de aprendizaje.
        builder.HasOne(s => s.Capability)
            .WithMany()
            .HasForeignKey(s => s.CapabilityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.PersonId);
        builder.HasIndex(s => s.CapabilityId);
        builder.HasIndex(s => new { s.PersonId, s.CapabilityId });
    }
}
