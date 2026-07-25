using HumanOS.Models.People;

namespace HumanOS.Models.Capabilities;

/// <summary>
/// Explicit "Inscribirme" record for a standalone Capability (i.e. NOT
/// part of any Program) — the enrollment signal that lets us track real
/// statistics per subscription, mirroring the role
/// AcceptedRecommendation.ProgramId plays for "Mis Programas" (2026-07-24
/// design decision). Deliberately separate from just running/practicing a
/// course: /capabilities' "Empezar" button stays free for anyone to try any
/// course WITHOUT ever creating this row — only the explicit "Inscribirme"
/// action does. Progress itself is NEVER duplicated here — it's always
/// read live from GraphProgressionEngine (see CapabilityEnrollmentService),
/// same as Programs, so there is a single source of truth for progress.
/// </summary>
public sealed class CapabilityEnrollment
{
    public Guid CapabilityEnrollmentId { get; set; } = Guid.NewGuid();

    /// <summary>FK: Person (estudiante) inscrito.</summary>
    public Guid PersonId { get; set; }

    /// <summary>FK: Capability en la que se inscribió, de forma standalone
    /// (sin Program).</summary>
    public Guid CapabilityId { get; set; }

    /// <summary>Fecha UTC en que se inscribió — mostrada como "assigned"/
    /// enrollment date en "Mis Capacidades", igual que AcceptedDate en
    /// "Mis Programas".</summary>
    public DateTime EnrolledDate { get; set; }

    public Person Person { get; set; } = null!;

    public Capability Capability { get; set; } = null!;
}
