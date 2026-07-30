using HumanOS.Models.People;

namespace HumanOS.Models.Programs;

/// <summary>
/// Explicit "Inscribirme" record for a Program, created directly from the
/// catalog (/programs/catalog) — mirrors <c>CapabilityEnrollment</c>'s role
/// for standalone Capabilities (2026-07-25). This is a SECOND path into
/// "Mis Programas" alongside the pre-existing Growth Plan Starting Point
/// recommendation-acceptance flow (see GrowthPlanService.GetEnrolledProgramsAsync,
/// which merges both sources by ProgramId so the list is always unified).
/// Progress is NEVER duplicated here — always read live via
/// GraphProgressionEngine per Capability, same as CapabilityEnrollment.
/// </summary>
public sealed class ProgramEnrollment
{
    public Guid ProgramEnrollmentId { get; set; } = Guid.NewGuid();

    /// <summary>FK: Person (estudiante) inscrito.</summary>
    public Guid PersonId { get; set; }

    /// <summary>FK: Program en el que se inscribió directamente desde el
    /// catálogo.</summary>
    public Guid ProgramId { get; set; }

    /// <summary>Fecha UTC en que se inscribió — mostrada como "assigned"/
    /// enrollment date en "Mis Programas" cuando este es el origen de la
    /// inscripción (en vez de una recomendación aceptada).</summary>
    public DateTime EnrolledDate { get; set; }

    public Person Person { get; set; } = null!;

    public LearningProgram Program { get; set; } = null!;
}
