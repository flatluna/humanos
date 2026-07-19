using System.Text.Json.Serialization;

namespace HumanOS.Agents.Runtime;

/// <summary>
/// A pedagogical mode of the Tutor Agent (fixed Paso 5, 2026-07-14 — see
/// /memories/repo/human-os-runtime-design.md). Deliberately NOT wired
/// through Harness's experimental <c>AgentSkillsProvider</c>/
/// <c>AgentSkillsSource</c> mechanism — a Skill here is Runtime-selected
/// guidance text injected into the per-turn prompt
/// (<see cref="TutorAgent"/>), same "Runtime decides, Tutor receives"
/// shape already used for <see cref="TutorToolPermissions"/>. Matches the
/// original Human OS Studio vision's Skill list (RecallSkill,
/// PredictionSkill, ApplicationSkill, ConfidenceSkill, IndependenceSkill,
/// ReflectionSkill) minus AssessmentSkill, which is Paso 6's concern.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TutorSkill
{
    /// <summary>Applies during <see cref="RuntimeStage.RecallRequired"/>.</summary>
    Recall,

    /// <summary>Applies during <see cref="RuntimeStage.PredictionRequired"/>.</summary>
    Prediction,

    /// <summary>Applies during <see cref="RuntimeStage.LearnerProduction"/>
    /// when the module's <see cref="RuntimePedagogicalContract.TargetMetric"/>
    /// is <see cref="Studio.CapabilityMetric.Application"/>.</summary>
    Application,

    /// <summary>Applies during <see cref="RuntimeStage.LearnerProduction"/>
    /// when TargetMetric is <see cref="Studio.CapabilityMetric.Confidence"/>.</summary>
    Confidence,

    /// <summary>Applies during <see cref="RuntimeStage.LearnerProduction"/>
    /// when TargetMetric is <see cref="Studio.CapabilityMetric.Independence"/>.</summary>
    Independence,

    /// <summary>Applies during <see cref="RuntimeStage.Reflection"/>.</summary>
    Reflection
}

/// <summary>
/// The actual guidance text for each <see cref="TutorSkill"/> — the
/// Skill's "content". Kept as plain static strings, deliberately NOT
/// SKILL.md files on disk (per explicit user preference 2026-07-14: a
/// Runtime-owned source, not files distributed across the system).
/// </summary>
public static class TutorSkillLibrary
{
    public static string InstructionsFor(TutorSkill skill) => skill switch
    {
        TutorSkill.Recall =>
            "Estás en la etapa de Recuperación (Recall), que ahora ocurre DESPUÉS de que el " +
            "alumno ya recibió la Instrucción (fixed 2026-07-16: enseñar primero, recordar " +
            "después). Pide al alumno que recuerde SIN volver a mirar el contenido de la etapa " +
            "anterior — de memoria, sin consultar notas, pistas ni IA. No repitas ni reveles de " +
            "nuevo el contenido aquí. Si el alumno pide ayuda antes de intentarlo, anímalo a " +
            "intentar primero; solo si genuinamente se atasca tras un intento real, ofrece la " +
            "pista MÍNIMA posible sin revelar el contenido completo de nuevo. Haz UNA sola " +
            "pregunta corta y conversacional (fixed 2026-07-17 — nunca una lista numerada ni " +
            "varios sub-puntos, aunque el contenido enseñado tenga varias partes: pide recordar " +
            "todo junto, en una frase, no como cuestionario).",

        TutorSkill.Prediction =>
            "Estás en la etapa de Predicción, que ocurre DESPUÉS de Recall y de la Instrucción " +
            "ya enseñada. Pide al alumno que prediga cómo le irá al aplicar lo aprendido en la " +
            "práctica (por ejemplo, qué resultado espera, qué le resultará más difícil) ANTES de " +
            "que realmente lo intente en la etapa de Práctica. No confirmes, corrijas ni valides " +
            "su predicción todavía — esa comparación (predicción vs. lo que realmente ocurre) " +
            "sucede después, en la etapa de Reflexión. Formula UNA sola pregunta corta y " +
            "conversacional (fixed 2026-07-17 — NUNCA una lista numerada ni un cuestionario de " +
            "varios puntos/incisos, aunque el tema tenga varias aristas: elige la más importante " +
            "y pregunta solo esa, como lo haría un tutor humano hablando en voz alta).",

        TutorSkill.Application =>
            "El alumno debe aplicar lo aprendido a un caso concreto y producir un artefacto " +
            "observable. NUNCA produzcas tú ese artefacto — pide explícitamente que lo haga el " +
            "alumno. Da retroalimentación sobre lo que el alumno ya produjo, nunca sustituyas su " +
            "producción con la tuya.",

        TutorSkill.Confidence =>
            "El alumno debe calibrar su propia confianza. Pídele que declare su nivel de " +
            "confianza ANTES de saber si su respuesta/producción fue correcta, y luego compara esa " +
            "confianza declarada con su desempeño real. El objetivo es calibración honesta, no " +
            "solo ánimo o validación automática.",

        TutorSkill.Independence =>
            "El alumno debe demostrar que puede hacerlo SIN apoyo — sin ejemplos, pistas, " +
            "checklists ni asistencia de IA. No ofrezcas andamiaje en este momento. Si el alumno " +
            "lo pide, recuérdale (con amabilidad) que esta etapa mide específicamente su autonomía " +
            "real, sin ayuda.",

        TutorSkill.Reflection =>
            "Guía al alumno a reflexionar de forma metacognitiva: qué predijo, qué ocurrió " +
            "realmente, y qué patrón de esto se transfiere a otras situaciones. No apures esta " +
            "etapa ni la conviertas en una nueva instrucción — es reflexión, no enseñanza.",

        _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown TutorSkill.")
    };
}
