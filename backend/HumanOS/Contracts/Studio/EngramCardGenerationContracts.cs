namespace HumanOS.Contracts.Studio;

/// <summary>
/// Input contract for <see cref="HumanOS.Agents.Studio.StructuredLearningEngramCardGenerationAgent"/>
/// — the YoungLearner-only "Aprende" Engram Card generator (2026-10-08). Only
/// ever called for documents whose <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningDocument.PedagogicalAudience"/>
/// is "YoungLearner"; never for "Standard" (enterprise) documents, which keep
/// using <see cref="HumanOS.Agents.Studio.StructuredLearningProfessorLectureAgent"/>'s
/// existing Scenes output unchanged.
///
/// Mirrors <see cref="StructuredLearningKnowledgeFactGenerationRequest"/>'s
/// "safe, serializable Knowledge Graph projection" convention: the agent
/// never invents an Entity or Fact — it only ever selects from, rephrases in
/// "Profe"'s voice, and sequences the ones it is given here.
/// </summary>
public sealed class EngramCardGenerationRequest
{
    public Guid EngramId { get; set; }

    public string EngramName { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    /// <summary>The Engram's own teaching source text (its Teaching step's
    /// narration/script, e.g. StructuredLearningProfessorLectureResult's
    /// SpokenLectureScript, or the section's raw Text as a fallback) —
    /// added 2026-10-08 so the agent can ground Concept cards directly in
    /// concrete examples the lesson ITSELF already states (e.g. "el perro
    /// dice guau, la vaca mu, la oveja be") even when no separate approved
    /// StructuredLearningEntity/KnowledgeFact row exists yet for them —
    /// same "SourceText is the source of truth for which ideas are
    /// essential" principle <see cref="StructuredLearningKnowledgeFactGenerationRequest"/>
    /// already uses. Optional — empty when unavailable, in which case the
    /// agent falls back to Entities/Facts only.</summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>Already-approved entities for this Engram (e.g. "Perro",
    /// "Gato") — the only valid source of Concept-type cards.</summary>
    public List<EngramCardEntityInput> Entities { get; set; } = [];

    /// <summary>Already-approved Knowledge Facts for this Engram — the only
    /// valid source of a card's <see cref="HumanOS.Models.Capabilities.Studio.EngramCard.Facts"/>
    /// text. The agent must copy (not paraphrase into new claims) each
    /// Statement it uses.</summary>
    public List<EngramCardFactInput> Facts { get; set; } = [];
}

/// <summary>Safe entity projection — same shape/purpose as
/// <see cref="StructuredLearningKnowledgeFactEntityInput"/>.</summary>
public sealed class EngramCardEntityInput
{
    public Guid EntityId { get; set; }

    public string CanonicalName { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>Safe Knowledge Fact projection, pre-resolved to the Entities it
/// is about (by the calling service, from
/// <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningKnowledgeFactGraphPathStep"/> →
/// Connection → Source/TargetEntityId — the agent itself never touches
/// Connections).</summary>
public sealed class EngramCardFactInput
{
    public Guid KnowledgeFactId { get; set; }

    public string Statement { get; set; } = string.Empty;

    public string FactType { get; set; } = string.Empty;

    /// <summary>EntityId(s) (from the request's <see cref="EngramCardGenerationRequest.Entities"/>)
    /// this fact is about. Empty when the fact could not be resolved to a
    /// specific entity — the agent should then not use it for a Concept
    /// card's Facts list.</summary>
    public List<Guid> RelatedEntityIds { get; set; } = [];
}

/// <summary>Output contract — a proposed, ordered set of Engram Cards for one
/// Engram's "Aprende" step. Never persisted directly: the calling service
/// maps each <see cref="EngramCardGenerated"/> onto a real
/// <see cref="HumanOS.Models.Capabilities.Studio.EngramCard"/>, filling in
/// IllustrationId/AudioStoragePath only after media generation succeeds —
/// same two-phase pattern already used for Scenes
/// (<see cref="StructuredLearningProfessorLectureResult"/> → <see cref="HumanOS.Services.StructuredLearningProfessorLectureService"/>).</summary>
public sealed class EngramCardGenerationResponse
{
    public Guid EngramId { get; set; }

    public string EngramName { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    /// <summary>Ordered sequence the student must go through, locked, one at
    /// a time (never more than 8 — see the agent's Instructions for why).</summary>
    public List<EngramCardGenerated> Cards { get; set; } = [];
}

/// <summary>One proposed card. IllustrationId/AudioStoragePath are
/// intentionally absent — assigned only after media generation, same
/// reasoning as <see cref="StructuredLearningKnowledgeFactGenerationResponse"/>
/// omitting KnowledgeFactId until persistence.</summary>
public sealed class EngramCardGenerated
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Sound { get; set; }

    public string? Verb { get; set; }

    /// <summary>See <see cref="HumanOS.Models.Capabilities.Studio.EngramCard.RecognitionCue"/>.</summary>
    public string? RecognitionCue { get; set; }

    public string? ImagePrompt { get; set; }

    /// <summary>What "Profe" says for this card, in first person, as if
    /// physically present with the child (same register as
    /// <see cref="HumanOS.Agents.Studio.StructuredLearningProfessorLectureAgent"/>'s
    /// KidsInstructions) — this is what gets synthesized into
    /// AudioStoragePath later. For Type=Concept cards (2026-10-08), this
    /// MUST be exactly <see cref="Explanation"/> + " " + <see cref="Example"/>
    /// (enforced in ValidateResponse) — the agent must never let a Concept
    /// card jump straight from greeting to question without first
    /// explaining the concept and then giving its concrete example.</summary>
    public string? ProfessorLine { get; set; }

    /// <summary>Required for Type=Concept cards only (2026-10-08) — ONE
    /// short sentence explaining WHAT the concept is, in Profe's voice,
    /// BEFORE the example (e.g. "Los animales hacen un sonido especial
    /// para comunicarse."). Null for every other card type.</summary>
    public string? Explanation { get; set; }

    /// <summary>Required for Type=Concept cards only (2026-10-08) — ONE
    /// short sentence giving the CONCRETE example demonstrating
    /// <see cref="Explanation"/>, in Profe's voice, right after it (e.g.
    /// "Escucha: el perro dice guau guau."). Null for every other card
    /// type.</summary>
    public string? Example { get; set; }

    public List<string> Facts { get; set; } = [];

    public string? Question { get; set; }

    public List<string>? Children { get; set; }

    public Guid? SourceEntityId { get; set; }

    public List<Guid> SourceKnowledgeFactIds { get; set; } = [];

    /// <summary>See <see cref="HumanOS.Models.Capabilities.Studio.EngramCard.IsComposedExample"/>.</summary>
    public bool IsComposedExample { get; set; }

    /// <summary>See <see cref="HumanOS.Models.Capabilities.Studio.EngramCard.PatternSourceFactId"/>.</summary>
    public Guid? PatternSourceFactId { get; set; }

    /// <summary>See <see cref="HumanOS.Models.Capabilities.Studio.EngramCard.Evidence"/>.</summary>
    public string? Evidence { get; set; }

    /// <summary>See <see cref="HumanOS.Models.Capabilities.Studio.EngramCard.SoundEffectLabel"/>
    /// — extracted from an explicit "Sonido a reproducir:"/"Sound to play:"
    /// annotation in SourceText when present. Null/absent for the common
    /// case of cards with no associated real sound effect.</summary>
    public string? SoundEffectLabel { get; set; }
}
