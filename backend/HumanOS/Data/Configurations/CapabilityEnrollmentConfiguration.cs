using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityEnrollmentConfiguration : IEntityTypeConfiguration<CapabilityEnrollment>
{
    public void Configure(EntityTypeBuilder<CapabilityEnrollment> builder)
    {
        builder.ToTable("CapabilityEnrollment", "dbo");

        builder.HasKey(x => x.CapabilityEnrollmentId)
            .HasName("PK_CapabilityEnrollment");

        builder.Property(x => x.CapabilityEnrollmentId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.EnrolledDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        // FK a Person — nunca cascadear el borrado (misma razón que
        // LearningSession: preservar historial de inscripción).
        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // FK a Capability — mismo criterio.
        builder.HasOne(x => x.Capability)
            .WithMany()
            .HasForeignKey(x => x.CapabilityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Una persona sólo puede tener UNA inscripción activa por Capability.
        builder.HasIndex(x => new { x.PersonId, x.CapabilityId })
            .IsUnique()
            .HasDatabaseName("UX_CapabilityEnrollment_Person_Capability");
    }
}
