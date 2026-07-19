using System;
using HumanOS.Models.Capabilities;
using HumanOS.Models.People;

namespace HumanOS.Models.Learning;

/// <summary>
/// Runtime V1, Paso 1 (2026-07-17) — represents one student's real,
/// in-progress (or finished) attempt at learning a Capability through its
/// CapabilityGraph. This is the root of the "Student Session" model:
///
///   LearningSession → LearningSessionNode → LearningSessionStep → LearningEvidence
///                                        └→ LearningAssessmentResult
///
/// Deliberately separate from the OLD Workflow-orchestrated
/// <c>RuntimeSession</c>/<c>TutorAgent</c> machinery (Agents/Runtime,
/// Agentic/Runtime) built for the flat CapabilityModule pipeline — this is
/// the NEW, simpler, table-only foundation for the graph pipeline
/// (CapabilityGraphNode/NodeExperienceBlueprint/BlueprintValidation). No
/// Agent Framework Workflow, no voice, no realtime, no Tutor, no graph
/// unlocking/mastery — those are explicitly deferred to later Runtime
/// Pasos. This Paso only persists what the student did.
/// </summary>
public class LearningSession
{
    /// <summary>Identificador único de la sesión (GUID).</summary>
    public Guid LearningSessionId { get; set; } = Guid.NewGuid();

    /// <summary>FK: Person (estudiante) dueño de esta sesión.</summary>
    public Guid PersonId { get; set; }

    /// <summary>FK: Capability que el estudiante está aprendiendo en esta sesión.</summary>
    public Guid CapabilityId { get; set; }

    /// <summary>Estado actual de la sesión.</summary>
    public LearningSessionStatus Status { get; set; } = LearningSessionStatus.NotStarted;

    /// <summary>Fecha UTC en que la sesión pasó a Active. Null mientras esté NotStarted.</summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>Fecha UTC en que la sesión terminó (Completed o Abandoned). Null mientras siga en curso.</summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>Fecha UTC de creación de la fila.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al Person (estudiante) dueño de esta sesión.</summary>
    public virtual Person? Person { get; set; }

    /// <summary>Referencia a la Capability que se está aprendiendo.</summary>
    public virtual Capability? Capability { get; set; }

    /// <summary>Progreso del estudiante por cada nodo del grafo visitado en esta sesión.</summary>
    public virtual ICollection<LearningSessionNode> Nodes { get; set; } = new List<LearningSessionNode>();
}
