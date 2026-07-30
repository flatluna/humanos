using System;
using HumanOS.Models.Capabilities.Graph;

namespace HumanOS.Models.Learning;

/// <summary>
/// Represents ONE real execution of a Memory Paradox step (Hypothesis,
/// Teaching, Recall, Production or Assessment) for a student, within a
/// <see cref="LearningSessionNode"/>. Reuses <see cref="ExperienceStepType"/>
/// (the same enum NodeExperienceBlueprintStep uses) so a step here maps 1:1
/// back to the blueprint step it executes.
/// </summary>
public class LearningSessionStep
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid LearningSessionStepId { get; set; } = Guid.NewGuid();

    /// <summary>FK: LearningSessionNode al que pertenece este step.</summary>
    public Guid LearningSessionNodeId { get; set; }

    /// <summary>Tipo de step del Memory Paradox (Hypothesis/Teaching/Recall/Production/Assessment).</summary>
    public ExperienceStepType StepType { get; set; }

    /// <summary>Estado actual de este step.</summary>
    public LearningSessionStepStatus Status { get; set; } = LearningSessionStepStatus.NotStarted;

    /// <summary>Fecha UTC en que el estudiante empezó este step. Null mientras esté NotStarted.</summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>Fecha UTC en que el estudiante terminó este step. Null mientras siga en curso.</summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>Fecha UTC de creación de la fila.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Solo para StepType=Recall: la pregunta/prompt dinámica que el
    /// TutorAgent generó más recientemente para este step (lo que el
    /// estudiante está viendo/respondiendo AHORA). Persistida server-side
    /// (no solo en memoria del frontend) para que un reload/reingreso a la
    /// página no pierda el contexto de "qué se está preguntando" — sin
    /// esto, tras un refresh se volvía a mostrar el contenido genérico del
    /// blueprint y el próximo intento se enviaba con tutorPromptShown=null,
    /// dejando al TutorAgent sin saber a qué pregunta estaba respondiendo
    /// el estudiante. Null antes del primer intento (se usa el contenido
    /// del blueprint) y se resetea a null cuando el step de Recall se
    /// reactiva tras un ciclo de regresión a Teaching.
    /// </summary>
    public string? CurrentRecallPrompt { get; set; }

    /// <summary>
    /// Solo para StepType=DeepDive: índice 0-based (dentro de
    /// NodeExperienceBlueprintStep.ExampleCardsJson, en orden) de la card
    /// que el estudiante debe dominar AHORA. El step solo se completa
    /// cuando este índice llega a ExampleCards.Count — a diferencia de
    /// Recall (dominar N items DISTINTOS de un pool abierto), Deep Dive
    /// exige dominar TODAS las cards, estrictamente en orden (decisión de
    /// producto, 2026-07-29: "no puede pasar al siguiente sin aprender
    /// cada card secuencialmente, de card en card tiene que hacer todas").
    /// Se resetea a 0 cuando el step se activa por primera vez.
    /// </summary>
    public int CurrentDeepDiveCardIndex { get; set; }

    /// <summary>
    /// Solo para StepType=DeepDive: la pregunta dinámica que el TutorAgent
    /// generó más recientemente sobre la card actual (mismo motivo de
    /// reload-safety que <see cref="CurrentRecallPrompt"/>). Null antes del
    /// primer intento de cada card, y se resetea a null cada vez que se
    /// avanza a una nueva card.
    /// </summary>
    public string? CurrentDeepDiveQuestion { get; set; }

    // === Navigation Properties ===

    /// <summary>Referencia al LearningSessionNode padre.</summary>
    public virtual LearningSessionNode? LearningSessionNode { get; set; }

    /// <summary>Respuestas/evidencia real que el estudiante produjo durante este step.</summary>
    public virtual ICollection<LearningEvidence> Evidence { get; set; } = new List<LearningEvidence>();
}
