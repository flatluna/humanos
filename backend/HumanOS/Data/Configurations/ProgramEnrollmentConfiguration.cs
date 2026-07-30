using HumanOS.Models.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ProgramEnrollmentConfiguration : IEntityTypeConfiguration<ProgramEnrollment>
{
    public void Configure(EntityTypeBuilder<ProgramEnrollment> builder)
    {
        builder.ToTable("ProgramEnrollment", "dbo");

        builder.HasKey(x => x.ProgramEnrollmentId)
            .HasName("PK_ProgramEnrollment");

        builder.Property(x => x.ProgramEnrollmentId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.ProgramId)
            .IsRequired();

        builder.Property(x => x.EnrolledDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        // FK a Person — nunca cascadear el borrado (misma razón que
        // CapabilityEnrollment: preservar historial de inscripción).
        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // FK a Program — mismo criterio.
        builder.HasOne(x => x.Program)
            .WithMany()
            .HasForeignKey(x => x.ProgramId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Una persona sólo puede tener UNA inscripción activa por Program.
        builder.HasIndex(x => new { x.PersonId, x.ProgramId })
            .IsUnique()
            .HasDatabaseName("UX_ProgramEnrollment_Person_Program");
    }
}
