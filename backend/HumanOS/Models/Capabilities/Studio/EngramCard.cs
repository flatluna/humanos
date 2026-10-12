namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One "Engram Card" shown during the YoungLearner "Aprende" sequential
/// learning experience (2026-10-08) — ONLY used when the owning
/// <see cref="StructuredLearningDocument.PedagogicalAudience"/> is
/// "YoungLearner" (K-12/early-childhood academic courses). Never shown and
/// never generated for "Standard" (enterprise) documents, which keep using
/// the existing single long-form Teaching step unchanged.
///
/// A Card is the atomic unit of a concept (e.g. "Perro", "Gato") that can be
/// heard, seen, and asked about, one at a time, in a fixed sequence the
/// student cannot skip ahead of (see
/// <see cref="StructuredLearningSectionStep.CurrentDeepDiveCardIndex"/>-style
/// progress field, added separately for Teaching/Cards use).
///
/// Stored as a JSON array under the "Cards" key inside
/// <see cref="StructuredLearningSectionStep.StepDataJson"/> for
/// StepType=Teaching — same lightweight "metadata-as-JSON-blob" convention
/// already used for "Scenes" (narrated-lecture scene breakdown) and for
/// <see cref="HumanOS.Models.Capabilities.Graph.ExampleCard"/> (no separate
/// DB table per sub-item type). A Teaching step's StepDataJson can carry
/// BOTH "Scenes" and "Cards" at once — the frontend prefers "Cards" when
/// present (YoungLearner path), falling back to "Scenes" otherwise.
///
/// IMPORTANT — alignment with Studio's approved Knowledge Graph: a Card's
/// Facts/Title/Type should be DERIVED FROM (not independently invented
/// from) the section's own <see cref="StructuredLearningEntity"/> and
/// <see cref="StructuredLearningKnowledgeFact"/> rows whenever a matching
/// entity exists, via <see cref="SourceEntityId"/>/<see cref="SourceKnowledgeFactIds"/>.
/// This guarantees the card can never silently drift from what Studio's
/// reviewer already approved for this Engram.
/// </summary>
public sealed class EngramCard
{
    /// <summary>Short, stable, human-readable slug for this card within its
    /// section (e.g. "dog", "cat") — unique only within the owning step's
    /// card list, NOT a database key. Used for ordering/debugging and so a
    /// reviewer's manual edits survive a regeneration diff.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Card headline shown to the student, e.g. "Perro".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Coarse card kind — drives which "Experiencia" layout the
    /// student-facing player renders. Extensible — any string value is
    /// accepted, but the player currently understands:
    /// "Concept" (single atomic concept, e.g. an animal),
    /// "Grouping" (a listen-only recap/review of 2+ earlier cards,
    /// referenced via <see cref="Children"/> for traceability only — NEVER
    /// an interactive tap-to-select game: the student cannot touch/select
    /// anything in this experience, only listen and watch, see
    /// <see cref="ProfessorLine"/>'s own doc for how a Grouping card must
    /// self-contain its own question AND answer, spoken aloud by Profe),
    /// "Skill" (a meta learning/behavior skill, e.g. "listen before you
    /// speak" — no Sound/Facts expected),
    /// "SocialSkill" (turn-taking, sharing, etc. — usually paired with a
    /// short Story),
    /// "Story" (narrative framing card, usually first or last in the
    /// sequence),
    /// "Vocabulary" (a small reference table of related terms, e.g. animal
    /// sounds verbs).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Onomatopoeia / short sound label read aloud and shown as
    /// text, e.g. "Guau". Null for cards with no inherent sound (Skill,
    /// SocialSkill, Story, Vocabulary, Grouping types).</summary>
    public string? Sound { get; set; }

    /// <summary>The verb naming the action/sound, e.g. "Ladrar" for Perro.
    /// Null when not applicable.</summary>
    public string? Verb { get; set; }

    /// <summary>Domain-agnostic recognition clue (2026-10-08) — a short,
    /// standalone spoken phrase that identifies THIS Concept card WITHOUT
    /// naming the answer directly, authored by the card-generation agent
    /// itself (never hardcoded by this platform, since a lesson may be
    /// about animal sounds, math facts, shapes, letters, or anything else).
    /// Only required on a Concept card when some Grouping card in the same
    /// sequence references it via <see cref="Children"/>. The Grouping
    /// card's interactive game (platform-implemented — student TAPS one of
    /// the full-size referenced cards shown as options) plays ONE
    /// referenced card's RecognitionCue audio at random and the student
    /// must tap the matching option. Examples the agent might produce
    /// depending on the lesson's own subject: an animal's sound ("Guau,
    /// guau"), a math fact stated without its result ("Dos más dos"), a
    /// shape's defining trait ("Tengo tres lados"), a letter's sound
    /// ("Hace el sonido mmm"). Null for cards never referenced by a
    /// Grouping card.</summary>
    public string? RecognitionCue { get; set; }

    /// <summary>Synthesized audio for <see cref="RecognitionCue"/> — a
    /// separate clip (same per-card storage convention as
    /// <see cref="AudioStoragePath"/>) so playing it alone during the
    /// Grouping game never also plays this card's full Explanation and
    /// gives away the answer. Null until synthesized or when
    /// RecognitionCue is null.</summary>
    public string? RecognitionCueAudioStoragePath { get; set; }

    /// <summary>Real-world, non-human-speech sound effect this card needs
    /// (2026-10-08) — e.g. "Mugido de vaca", "Sonido de tren", "Dos
    /// palmadas". Extracted from an explicit "Sonido a reproducir:"/"Sound
    /// to play:" annotation in the card's SourceText when present. Never a
    /// human voice saying words (that's already covered by
    /// <see cref="ProfessorLine"/>'s own narration audio) — null/absent
    /// whenever this card has no associated sound effect, which is the
    /// common case for most subjects (e.g. math, shapes, letters).</summary>
    public string? SoundEffectLabel { get; set; }

    /// <summary>Generated sound-effect audio for <see cref="SoundEffectLabel"/>
    /// (via <see cref="HumanOS.Services.ElevenLabsSoundEffectService"/>),
    /// stored standalone so it can be replayed/reused on its own (e.g. by a
    /// future recognition game), separate from the combined narration+effect
    /// clip stored at <see cref="AudioStoragePath"/>. Null until generated,
    /// when the service isn't configured, or when SoundEffectLabel is null.</summary>
    public string? SoundEffectStoragePath { get; set; }

    /// <summary>Short, concrete, child-friendly description used as the
    /// image-generation prompt for this card's illustration (same style
    /// conventions as Scenes' ImagePrompt — no text/letters/numbers in the
    /// image). Null until an illustration is generated or requested.</summary>
    public string? ImagePrompt { get; set; }

    /// <summary>Generated illustration for this card, if any — references a
    /// StructuredLearningIllustration row, fetched the same way as every
    /// other illustration in this feature (GET .../illustrations/{id}/image).
    /// Null when generation failed, isn't configured, or hasn't run yet.</summary>
    public Guid? IllustrationId { get; set; }

    /// <summary>Short audio clip of "Profe" (always that name, first-person,
    /// as if physically present with the child — see
    /// <see cref="HumanOS.Agents.Studio.StructuredLearningProfessorLectureAgent"/>'s
    /// KidsInstructions register, reused verbatim for this card's narration)
    /// introducing/narrating this specific card. Storage path, same
    /// convention as Scenes' AudioStoragePath. When <see cref="SoundEffectLabel"/>
    /// is present, this is the COMBINED clip (narration followed by the real
    /// sound effect) — see <see cref="SoundEffectStoragePath"/> for the
    /// effect alone. Null until synthesized.</summary>
    public string? AudioStoragePath { get; set; }

    /// <summary>Short, kid-safe spoken line "Profe" says for this card
    /// (what <see cref="AudioStoragePath"/> is synthesized from), e.g.
    /// "Este es un perro. ¿Puedes decir guau?". Always first-person, always
    /// addressing the child directly — never third-person narration. For
    /// Type=Concept cards (2026-10-08) this is <see cref="Explanation"/> +
    /// " " + <see cref="Example"/>, concatenated — never just a bare
    /// greeting-to-question jump with no actual teaching content.</summary>
    public string? ProfessorLine { get; set; }

    /// <summary>Concept cards only (2026-10-08) — ONE short sentence
    /// explaining WHAT the concept is, spoken by Profe, BEFORE the concrete
    /// Example. Shown to the student as its own distinct line (💡) so the
    /// card always teaches explanation-then-example, never jumps straight
    /// to a question. Null for every other card type.</summary>
    public string? Explanation { get; set; }

    /// <summary>Concept cards only (2026-10-08) — ONE short sentence giving
    /// the CONCRETE example demonstrating <see cref="Explanation"/>, spoken
    /// by Profe right after it (e.g. "Escucha: el perro dice guau guau.").
    /// Shown to the student as its own distinct line. Null for every other
    /// card type.</summary>
    public string? Example { get; set; }

    /// <summary>Short, verifiable statements this card teaches, e.g.
    /// ["El perro dice guau", "El perro ladra", "Es una mascota común"].
    /// Whenever possible these should be copies of already-approved
    /// <see cref="StructuredLearningKnowledgeFact.Statement"/> values (see
    /// <see cref="SourceKnowledgeFactIds"/>), not freshly invented text, so
    /// the card can never contradict what Studio's Knowledge Graph already
    /// says about this concept.</summary>
    public List<string> Facts { get; set; } = [];

    /// <summary>Optional simple recall/recognition question shown after the
    /// card's sound+narration finish, e.g. "¿Qué animal hizo este sonido?".
    /// Null for cards that are pure exposition (Story, Skill framing).</summary>
    public string? Question { get; set; }

    /// <summary>Only meaningful for Type="Grouping" cards (e.g. "Sonidos de
    /// los animales") — the Ids of the earlier cards in this same sequence
    /// this card ties together, e.g. ["dog","cat","cow"]. Null/empty for
    /// every other card type.</summary>
    public List<string>? Children { get; set; }

    /// <summary>Traceability (REQUIRED whenever Type="Concept" and a
    /// matching entity exists) — the <see cref="StructuredLearningEntity.StructuredLearningEntityId"/>
    /// this card was derived from, so a later edit/regeneration of that
    /// entity in Studio's Knowledge Graph can be reflected here instead of
    /// silently drifting apart. Null only for cards with no 1:1 entity
    /// match (Skill/SocialSkill/Story/Vocabulary/Grouping types).</summary>
    public Guid? SourceEntityId { get; set; }

    /// <summary>Traceability — the specific <see cref="StructuredLearningKnowledgeFact.KnowledgeFactId"/>
    /// rows <see cref="Facts"/> were copied from, in the same order. Empty
    /// when a fact had no approved Knowledge Fact counterpart yet (freshly
    /// authored instead of derived — should be the exception, not the
    /// norm).</summary>
    public List<Guid> SourceKnowledgeFactIds { get; set; } = [];

    /// <summary>True when this card is a COMPOSED illustrative example
    /// (2026-10-08) — not a 1:1 derivation of an approved Entity/Fact, but a
    /// new concrete instance of a general PATTERN an approved Fact already
    /// establishes (e.g. the approved Fact "cada animal hace su sonido" plus
    /// one approved concrete instance "El Gato produce el sonido miau"
    /// together justify composing "El Perro produce el sonido guau" as a
    /// second instance of that same pattern, even though no approved Fact
    /// states it verbatim). Same philosophy already used for DeepDive's
    /// ExampleCards (<see cref="HumanOS.Agents.Studio.ExperienceDesignerAgent"/>'s
    /// "apply the general formula/theorem... to a NEW value beyond what was
    /// already shown") — composing, never fabricating an unrelated claim.
    /// Always false when <see cref="SourceKnowledgeFactIds"/> is non-empty;
    /// when true, <see cref="SourceEntityId"/> is null (no real approved
    /// Entity exists for it yet) and <see cref="PatternSourceFactId"/>
    /// records which approved Fact's general pattern justified it, so a
    /// Studio reviewer can audit/promote it later instead of it silently
    /// passing as Studio-approved content.</summary>
    public bool IsComposedExample { get; set; }

    /// <summary>Only meaningful when <see cref="IsComposedExample"/> is
    /// true — the <see cref="StructuredLearningKnowledgeFact.KnowledgeFactId"/>
    /// of the approved Fact whose general pattern this composed card is a
    /// new concrete instance of. Null for every non-composed card.</summary>
    public Guid? PatternSourceFactId { get; set; }

    /// <summary>Third provenance category (2026-10-08, alongside approved
    /// Entity/Fact derivation and IsComposedExample) — a literal, verbatim
    /// quote from the Engram's own teaching SourceText that directly
    /// supports this card's Sound/Verb/Facts (e.g. the lesson's narration
    /// already says "la vaca dice mu" even though no separate approved
    /// StructuredLearningEntity/KnowledgeFact row exists yet for "Vaca").
    /// Non-null exactly when this card is grounded in SourceText rather
    /// than in an approved Fact or a composed pattern — never null AND
    /// non-composed-AND-no-SourceKnowledgeFactIds at the same time (see
    /// the agent's ValidateResponse). Lets a Studio reviewer verify the
    /// claim against the lesson's own words instead of trusting it blindly.</summary>
    public string? Evidence { get; set; }
}
