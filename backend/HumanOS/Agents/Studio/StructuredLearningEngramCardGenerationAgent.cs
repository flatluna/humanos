using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// YoungLearner-only "Aprende" Engram Card generator (2026-10-08). Takes one
/// Engram's already-approved Entities + Knowledge Facts (same safe
/// projections <see cref="StructuredLearningKnowledgeFactGenerationAgent"/>
/// uses) and proposes a short, LOCKED, sequential list of
/// <see cref="EngramCardGenerated"/> — one atomic concept per card, narrated
/// in "Profe"'s first-person voice (same register as
/// <see cref="StructuredLearningProfessorLectureAgent"/>'s KidsInstructions).
///
/// Only ever invoked for documents whose PedagogicalAudience is
/// "YoungLearner" — never called for "Standard" (enterprise) documents,
/// which keep using StructuredLearningProfessorLectureAgent's Scenes output
/// unchanged. This agent never persists cards, generates illustrations, or
/// synthesizes audio — see the (future) persistence service for the
/// two-phase pattern already used for Scenes.
/// </summary>
public class StructuredLearningEngramCardGenerationAgent
{
    private static readonly IReadOnlySet<string> CardTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Concept", "Grouping", "Skill", "SocialSkill", "Story", "Vocabulary"
    };

    /// <summary>Hard cap on cards per Engram (product decision, 2026-10-08):
    /// mirrors StructuredLearningProfessorLectureAgent's own 3-6 Scenes cap
    /// for the same cost/attention-span reasons, with a slightly higher
    /// ceiling because each card is much shorter (one concept) than a scene
    /// (a narration beat). Above this, split the Engram into more than one
    /// "Aprende" session instead of cramming more cards into one.</summary>
    public const int MaxCards = 8;

    private const string Instructions = """
        You generate a short, ordered sequence of "Engram Cards" for the YoungLearner (early
        childhood / K-primary) "Aprende" learning experience. Return ONLY the requested
        EngramCardGenerationResponse object. Never return Markdown, commentary, or additional
        properties.

        ROLE (CRITICAL FRAMING): you are NOT writing lesson material for a human teacher to later
        read aloud to a class. YOU ARE "Profe", personally and directly conducting this class live,
        one-on-one, for ONE child, right now. Every Explanation/Example/ProfessorLine you write is
        words YOU are speaking out loud to that child in this exact moment — think and write as the
        teacher actually teaching, not as someone describing or summarizing a lesson for someone
        else to teach later. If a line would sound strange coming out of a real teacher's mouth
        while sitting with a child, rewrite it.

        ACTUAL DELIVERY CONTEXT (CRITICAL — SourceText may describe a DIFFERENT, group/classroom
        context than how this is actually delivered): this card sequence is experienced by exactly
        ONE child, ALONE, on a device, going through these cards one at a time in a fixed, LOCKED
        order — there is no teacher physically present, no other children, and no live "turn" to
        wait for within this experience itself. SourceText may come from a lesson originally
        designed for a group/classroom activity (e.g. a teacher playing a sound aloud to several
        children who take turns pointing at a card) — when it does, you must ADAPT any mechanic
        that only makes sense with other people/a live turn-taking game into something that
        actually makes sense for one child alone with a screen (e.g. reframe "espera tu turno" as
        a solo patience/attention skill like "escucha el sonido completo antes de responder" — NOT
        "wait for your turn", since there is no one else and no turn here). If a SourceText passage
        describes a group-only mechanic you cannot meaningfully adapt to a solo experience, OMIT it
        entirely rather than importing it verbatim and leaving the child confused about who/what
        they're supposed to wait for. Before writing any Skill/SocialSkill card, ask yourself: "does
        this make complete sense for ONE child alone with a screen, with no other person
        involved?" — if the honest answer is no, do not include it.

        THREE VALID SOURCES FOR A CONCEPT CARD'S CONTENT (every Concept card must use EXACTLY ONE):
        (A) GROUNDED BY APPROVED ENTITY+FACT (strongest, prefer whenever available): the card is
            about one of the supplied Entities, and its Facts are copied (not paraphrased into new
            claims) from supplied Facts whose RelatedEntityIds include that Entity. Set
            SourceEntityId=that EntityId, SourceKnowledgeFactIds=the Facts you copied from,
            IsComposedExample=false, Evidence=null.
        (B) GROUNDED DIRECTLY IN SourceText (2026-10-08 — use this whenever the lesson's own
            SourceText literally names a concrete example that (A) doesn't yet cover, e.g. SourceText
            says "la vaca dice mu" or "la oveja hace be (balido)" but no separate Entity/Fact row
            exists for Vaca/Oveja yet): set Evidence to a literal, verbatim, contiguous quote copied
            FROM SourceText (not reworded) that directly supports the card's Sound/Verb/claim —
            never a quote that only loosely relates. Set SourceEntityId=null,
            SourceKnowledgeFactIds=[], IsComposedExample=false. This is NOT inventing — it is citing
            what the lesson's own script already says, just not yet promoted to an approved Fact row.
        (C) COMPOSED EXAMPLE (narrowest, use only when neither A nor B applies — same philosophy as
            DeepDive's ExampleCards applying a stated general rule to a NEW value, e.g. "2+2=4"
            licenses composing "3+1=4" as a further worked example of the SAME rule — composing,
            never fabricating an unrelated claim): ONLY when a supplied Fact states a GENERAL
            PATTERN (typically FactType=Relationship or Principle, e.g. "cada animal hace su sonido
            cuando recibe una tarjeta de turno") AND at least one (A) or (B) card already gives ONE
            concrete grounded instance of that same pattern, you MAY compose AT MOST 2 additional
            Concept cards as further instances of that exact pattern, using extremely common,
            uncontroversial, universally-known world knowledge — NEVER an obscure or debatable
            instance. Set IsComposedExample=true, PatternSourceFactId=the pattern Fact's
            KnowledgeFactId, SourceEntityId=null, SourceKnowledgeFactIds=[], Evidence=null. NEVER
            compose more (C) cards than you have (A)+(B) grounded cards combined.
        A Concept card must satisfy EXACTLY ONE of (A), (B), (C) — never mix their fields (e.g.
        never set both SourceKnowledgeFactIds and Evidence on the same card).
        Non-Concept cards (Grouping/Skill/SocialSkill/Story/Vocabulary) set
        SourceEntityId/SourceKnowledgeFactIds/Evidence/PatternSourceFactId all to null/empty and
        IsComposedExample=false.

        CARD COUNT, SELECTION, AND QUALITY OVER PADDING:
        - Produce at most 8 cards total. Aim for AT LEAST 3 concrete Concept cards whenever
          SourceText or Facts support that many distinct concrete examples (e.g. SourceText naming
          several animals and their sounds) — do not under-deliver down to 1-2 cards when richer
          SourceText content is available; mine (B) generously in that case. But also never pad:
          when only 1-2 concrete examples are genuinely supported across (A)+(B)+(C), return only
          those — it is far better to return fewer cards a 5-year-old finds clear than to pad with
          abstract/confusing filler.
        - Prefer concrete, tangible things a child can see/hear/touch (an animal, an object, a
          person) over abstract process/mechanic entities (e.g. "the game itself", "a turn card", "a
          sound cue"). When the only supplied Entities are abstract game-mechanic concepts, mine
          SourceText (B) and the COMPOSED allowance (C) generously instead of turning each abstract
          mechanic entity into its own confusing "Concept" card — the student should walk away
          having clearly learned a small set of concrete things, not a procedural explanation of how
          the activity itself works.
        - Start with one "Concept" card per selected concrete instance (one animal/object/idea per
          card — never combine two into one Concept card).
        - After all Concept cards, you MAY add exactly one "Grouping" card that ties 2 or more of
          THIS SAME SET's Concept card Ids together as a simple recognition game (Children list),
          ONLY when there are at least 3 Concept cards. Never add a Grouping card referencing an Id
          that is not one of this response's own Concept card Ids. WHENEVER you add a Grouping
          card, EVERY Concept card it references via Children MUST itself set a non-null
          RecognitionCue (see CARD FIELDS below) — the Grouping card's own actual game mechanic
          (implemented by the platform, not by you) plays ONE referenced card's RecognitionCue
          audio at random and shows the referenced cards again, full-size, as tappable options; the
          student PHYSICALLY TAPS the one they think matches. Without a RecognitionCue on every
          referenced card the game cannot function. If you cannot write a good RecognitionCue for
          at least 3 of your Concept cards, do not add a Grouping card at all. The Grouping card's
          own ProfessorLine is just its short spoken INTRO (e.g. "Vamos a jugar. Escucha con
          atención y toca la imagen correcta.") — it must NOT itself ask-then-answer the question
          (unlike a plain recap card would); the actual question-and-answer happens through the tap
          interaction, not through narration.
        - You MAY add at most one "Skill" or "SocialSkill" card (a short meta-lesson like "listen
          before you answer" or "wait your turn") ONLY when the source Facts/SourceText actually
          support a behavioral/social lesson AND you already have at least 2 concrete Concept cards
          — never let it be the ONLY substantive content, never invent one out of thin air.
        - You MAY add at most one "Story" card (a short narrative frame, e.g. a 1-2 sentence scene
          tying the Concept cards together) as either the FIRST or LAST card — never both, never
          more than one.
        - Never produce a "Vocabulary" card unless Facts/SourceText explicitly describe a small set
          of parallel terms (e.g. several action verbs for several entities) — otherwise omit it.
        - Order matters: the student goes through cards strictly in the array order, locked, one at
          a time. Put the Story card (if any) first or last, Concept cards in a sensible teaching
          order, the Grouping card after its referenced Concept cards, and any Skill/SocialSkill
          card wherever it reads naturally (commonly last, as a closing lesson).

        CARD FIELDS:
        - Id: a short, unique (within this response), kebab-case or single-word slug, e.g. "dog".
        - Title: short headline shown to the student, in the requested Language.
        - Type: exactly one of Concept, Grouping, Skill, SocialSkill, Story, Vocabulary.
        - Sound/Verb: only for Concept cards about something that makes a characteristic sound
          (e.g. an animal) — the onomatopoeia and the verb naming it, e.g. Sound="Guau",
          Verb="Ladrar". Null for every other card AND null for Concept cards about something with
          no inherent sound (e.g. a math fact, a shape, a color) — never force a Sound/Verb value
          that doesn't genuinely fit the concept.
        - RecognitionCue (2026-10-08, DOMAIN-AGNOSTIC — this platform covers every subject, not
          only animal sounds, so NEVER limit this to onomatopoeia): required on every Concept card
          referenced by a Grouping card's Children (optional otherwise). A short, standalone spoken
          phrase — in Profe's voice, played ALONE later without any other context — that lets a
          child who already learned this card identify it WITHOUT naming the answer outright.
          Choose whatever form genuinely fits THIS card's own subject matter, e.g.: an animal's
          sound ("Guau, guau"), a math fact stated without its result ("Dos más dos"), a shape's
          defining trait ("Tengo tres lados y una punta"), a letter's sound ("Hace el sonido
          mmm"), a color's described appearance ("Soy el color del cielo"), a historical figure's
          best-known action stated plainly ("Descubrió la gravedad al ver caer una manzana"). The
          common requirement across every subject: concrete, unambiguous, and answerable by a
          child who learned ONLY this card's own Explanation/Example/Facts — never a cue vague
          enough to match more than one of this sequence's own cards.
        - ImagePrompt: short, concrete, child-friendly, third-person scene description for
          illustration (no text/letters/numbers in the image, no rendering/typography details) —
          required for every card. NEVER depict the child making a live request to "Profe" (no
          "niño levanta la mano para pedir repetir", no "child raises hand to ask") — same reason
          as the spoken-line rule above: there is no live channel for the child to ask Profe
          anything. Depict the child simply listening, looking, or responding instead.
        - Explanation + Example (REQUIRED for every Concept card, null for every other type,
          2026-10-08 — a Concept card must ALWAYS teach explanation-then-example, NEVER jump
          straight from a greeting to the recall Question with no actual teaching content):
          * Explanation: ONE short sentence, spoken by Profe directly to the child, explaining WHAT
            this concept/sound/idea IS in simple terms — e.g. "Los animales hacen un sonido especial
            para comunicarse. Este es un perro.".
          * Example: ONE short sentence, spoken by Profe right after Explanation, giving the
            CONCRETE example/demonstration — e.g. "Escucha: el perro dice guau, guau.".
          * ProfessorLine MUST equal Explanation + " " + Example, exactly (this concatenation is
            what gets synthesized into audio) — never add extra sentences beyond these two, never
            omit either half.
        - ProfessorLine (non-Concept cards only): 1-2 short sentences "Profe" says DIRECTLY TO the
          child, first person, as if physically present with them right now. Explanation/Example
          stay null for these card types.
        - EVERY CARD's spoken line(s) — first person, as if physically present with the child (same
          warm register as a parent reading a picture book aloud). THE VERY FIRST CARD's spoken
          line (Explanation if Concept, else ProfessorLine) MUST open with a warm, direct greeting
          to the child (e.g. "¡Hola! ¿Cómo estás?" / "Hi! How are you today?") before introducing
          content — every later card jumps straight into its own content (no repeated greeting).
          NEVER prefix any spoken line with a speaker label like "Profe:" — the UI already shows who
          is speaking separately; these fields must contain ONLY the words actually spoken, e.g.
          "Este es un perro. ¿Puedes decir guau?" (correct), never "Profe: Este es un perro..."
          (wrong). NEVER third-person narration ("this card shows..."), NEVER mention AI/lesson/
          activity in a clinical way. NEVER ask the child to make a live request back to "Profe"
          ("pídeme que lo repita", "ask me to play it again", "dime si quieres escucharlo otra
          vez") — there is NO live voice channel for the child to talk back; "Profe" is a
          pre-recorded clip, not a listening conversational agent. If a card needs to mention
          replaying a sound, phrase it as something the CHILD can do themselves (e.g. "si no lo
          escuchaste bien, puedes escucharlo otra vez" referring to a replay control, never a
          request directed at Profe).
        - Facts: 1-3 short statements. For (A)-grounded cards, copied verbatim from the supplied
          Facts. For (B)-grounded cards, a short claim directly supported by Evidence (may lightly
          rephrase Evidence into a clean sentence, but must not add anything Evidence doesn't say).
          For (C)-composed cards, a short claim about the composed instance (e.g. "El perro dice
          guau"). Empty for card types with no linked concept (Grouping/Skill/SocialSkill/Story/
          Vocabulary) unless genuinely supported.
        - Question: a short recognition/recall question shown after the card finishes, e.g.
          "¿Qué animal hizo este sonido?". Omit (null) for pure-exposition cards (typically Story).
        - Children: ONLY for Grouping cards — the Ids (from THIS response) of the Concept cards it
          ties together. Null for every other card type.
        - SourceEntityId/SourceKnowledgeFactIds/Evidence/PatternSourceFactId: see the THREE VALID
          SOURCES section above — set according to which of (A)/(B)/(C) this card uses.
        - SoundEffectLabel (2026-10-08): set ONLY when SourceText contains an explicit "Sonido a
          reproducir:"/"Sound to play:" annotation for this exact card's content, naming a
          CONCRETE NON-HUMAN-SPEECH sound effect (an animal, a machine, nature, an object, a
          physical impact like clapping) that a downstream service must generate/play, e.g.
          "Mugido de vaca", "Sonido de tren", "Dos palmadas". Copy that label (or a faithful
          short version of it) verbatim-ish — never invent a sound effect SourceText doesn't
          mention. Leave null whenever: (a) SourceText has no such annotation for this card, or
          (b) the annotation says "Ninguno"/"None", or (c) the "sound" is just a voice saying
          words (e.g. comparing "sol" vs "mesa") — that case is already fully covered by the
          card's own ProfessorLine/Explanation/Example text and needs no separate generation.

        LANGUAGE RULE (STRICT):
        Write every Title/ProfessorLine/Explanation/Example/Facts/Question/ImagePrompt in the
        requested Language. When Language is 'es', everything must be natural, simple, warm spoken
        Spanish a 5-to-6-year-old understands easily — never English, never mixed languages.

        The response must preserve EngramId, EngramName, and Language exactly as received. Do not
        generate Recall/Assessment content, mastery scoring, or persistence metadata — only the
        card sequence itself.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;
    private readonly bool _isReasoningModel;

    public StructuredLearningEngramCardGenerationAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            _modelName = string.Empty;
            return;
        }

        _modelName = deploymentName;
        _isReasoningModel = deploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
            && !deploymentName.Contains("chat", StringComparison.OrdinalIgnoreCase);

        var client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningEngramCardGenerationAgent");
    }

    public virtual bool IsConfigured => _agent is not null;

    public virtual async Task<EngramCardGenerationResponse> GenerateAsync(
        EngramCardGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (response, _) = await RunModelAsync(request, cancellationToken);
        ValidateResponse(request, response);
        return response;
    }

    /// <summary>Same call, also returning token usage — used to persist a
    /// StructuredLearningGenerationUsage row per call, same convention as
    /// <see cref="StructuredLearningKnowledgeFactGenerationAgent.GenerateRawWithUsageAsync"/>.</summary>
    public virtual async Task<(EngramCardGenerationResponse Response, AgentTokenUsage Usage)> GenerateWithUsageAsync(
        EngramCardGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (response, usage) = await RunModelAsync(request, cancellationToken);
        ValidateResponse(request, response);
        return (response, usage);
    }

    protected virtual async Task<(EngramCardGenerationResponse Response, AgentTokenUsage Usage)> RunModelAsync(
        EngramCardGenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "StructuredLearningEngramCardGenerationAgent is not configured. Verify 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName'.");
        }

        var langReminder = string.Equals(request.Language, "es", StringComparison.OrdinalIgnoreCase)
            ? "MANDATORY LANGUAGE: Language is 'es' (Spanish). Every card field MUST be written in warm, simple, natural spoken SPANISH a 5-to-6-year-old understands. Do NOT write in English.\n\n"
            : string.Empty;
        var prompt = langReminder + JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        ChatClientAgentRunOptions? runOptions = _isReasoningModel
            ? new ChatClientAgentRunOptions(new ChatOptions
            {
#pragma warning disable OPENAI001
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    ReasoningEffortLevel = ChatReasoningEffortLevel.Medium
                }
#pragma warning restore OPENAI001
            })
            : null;

        var response = await _agent.RunAsync<EngramCardGenerationResponse>(
            prompt, options: runOptions, cancellationToken: cancellationToken);
        var usage = response.Usage;
        return (response.Result, new AgentTokenUsage
        {
            AgentName = "StructuredLearningEngramCardGenerationAgent",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        });
    }

    /// <summary>Deterministic, non-AI validation of the model's output —
    /// same philosophy as <see cref="StructuredLearningKnowledgeFactGenerationAgent.ValidateResponse"/>:
    /// the LLM proposes, this method has final say on whether the shape is
    /// safe to persist.</summary>
    public static void ValidateResponse(
        EngramCardGenerationRequest request,
        EngramCardGenerationResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        if (response.EngramId != request.EngramId || response.EngramName != request.EngramName || response.Language != request.Language)
        {
            throw new InvalidOperationException("Engram Card response identity does not match the request.");
        }
        if (response.Cards.Count == 0 || response.Cards.Count > MaxCards)
        {
            throw new InvalidOperationException($"Engram Card response must contain between 1 and {MaxCards} cards (got {response.Cards.Count}).");
        }

        var entityIds = request.Entities.Select(entity => entity.EntityId).ToHashSet();
        var factsById = request.Facts.ToDictionary(fact => fact.KnowledgeFactId);
        var cardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedSourceText = NormalizeForEvidenceMatch(request.SourceText);

        foreach (var card in response.Cards)
        {
            if (string.IsNullOrWhiteSpace(card.Id) || !cardIds.Add(card.Id))
            {
                throw new InvalidOperationException("Engram Cards must have unique, non-empty Ids.");
            }
            if (string.IsNullOrWhiteSpace(card.Title) || !CardTypes.Contains(card.Type) || string.IsNullOrWhiteSpace(card.ImagePrompt))
            {
                throw new InvalidOperationException($"Engram Card '{card.Id}' has an invalid Title, Type, or ImagePrompt.");
            }

            if (card.Type == "Concept")
            {
                if (string.IsNullOrWhiteSpace(card.Explanation) || string.IsNullOrWhiteSpace(card.Example))
                {
                    throw new InvalidOperationException($"Concept card '{card.Id}' must set both Explanation and Example.");
                }
                var expectedProfessorLine = $"{card.Explanation} {card.Example}";
                if (!string.Equals(card.ProfessorLine?.Trim(), expectedProfessorLine.Trim(), StringComparison.Ordinal))
                {
                    // Self-heal rather than reject the whole batch (2026-10-09) — Explanation/
                    // Example are the validated, trusted content; ProfessorLine is merely their
                    // concatenation for TTS. A model-side whitespace/punctuation slip here
                    // shouldn't throw away an otherwise-good set of cards.
                    card.ProfessorLine = expectedProfessorLine.Trim();
                }
            }
            else if (!string.IsNullOrWhiteSpace(card.Explanation) || !string.IsNullOrWhiteSpace(card.Example))
            {
                throw new InvalidOperationException($"Card '{card.Id}' of type '{card.Type}' must not set Explanation/Example (Concept-only fields).");
            }

            foreach (var line in new[] { card.ProfessorLine, card.Explanation, card.Example, card.RecognitionCue })
            {
                if (line is not null && line.TrimStart().StartsWith("Profe:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Card '{card.Id}' must not prefix a spoken line with a 'Profe:' label.");
                }
            }
            if (card.Type != "Concept" && !string.IsNullOrWhiteSpace(card.RecognitionCue))
            {
                throw new InvalidOperationException($"Card '{card.Id}' of type '{card.Type}' must not set RecognitionCue (Concept-only).");
            }

            if (!string.IsNullOrWhiteSpace(card.SoundEffectLabel))
            {
                var normalizedLabel = card.SoundEffectLabel.Trim();
                if (normalizedLabel.Contains('"') || normalizedLabel.Contains('\u201C') ||
                    normalizedLabel.StartsWith("Voz de", StringComparison.OrdinalIgnoreCase) ||
                    normalizedLabel.StartsWith("Voice of", StringComparison.OrdinalIgnoreCase) ||
                    normalizedLabel.StartsWith("Diciendo", StringComparison.OrdinalIgnoreCase) ||
                    normalizedLabel.StartsWith("Saying", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedLabel, "Ninguno", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedLabel, "None", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Card '{card.Id}' SoundEffectLabel must be a concrete non-human-speech sound effect, never a voice saying words, and never a literal 'Ninguno'/'None' placeholder (leave it null instead).");
                }
            }

            if (card.IsComposedExample)
            {
                if (card.Type != "Concept")
                {
                    throw new InvalidOperationException($"Composed example card '{card.Id}' must be of Type Concept.");
                }
                if (card.SourceEntityId is not null || card.SourceKnowledgeFactIds.Count != 0 || !string.IsNullOrWhiteSpace(card.Evidence))
                {
                    throw new InvalidOperationException($"Composed example card '{card.Id}' must not set SourceEntityId, SourceKnowledgeFactIds, or Evidence.");
                }
                if (card.PatternSourceFactId is not Guid patternFactId || !factsById.ContainsKey(patternFactId))
                {
                    throw new InvalidOperationException($"Composed example card '{card.Id}' must reference a valid PatternSourceFactId.");
                }
            }
            else
            {
                if (card.PatternSourceFactId is not null)
                {
                    throw new InvalidOperationException($"Non-composed card '{card.Id}' must not set PatternSourceFactId.");
                }
                if (card.Type == "Concept")
                {
                    var hasEntityGrounding = card.SourceEntityId is Guid entityId && entityIds.Contains(entityId);
                    var hasTextEvidence = !string.IsNullOrWhiteSpace(card.Evidence);
                    if (hasEntityGrounding == hasTextEvidence)
                    {
                        // Must be EXACTLY one of (A) entity-grounded or (B) text-evidenced — never
                        // both (ambiguous provenance), never neither (ungrounded invention).
                        throw new InvalidOperationException(
                            $"Concept card '{card.Id}' must be grounded by EXACTLY ONE of a valid SourceEntityId or a literal Evidence quote from SourceText.");
                    }
                    if (hasTextEvidence && (normalizedSourceText.Length == 0 || !normalizedSourceText.Contains(NormalizeForEvidenceMatch(card.Evidence!), StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException($"Concept card '{card.Id}' Evidence is not a literal quote found in SourceText.");
                    }
                }
                else
                {
                    if (card.SourceEntityId is not null || !string.IsNullOrWhiteSpace(card.Evidence))
                    {
                        throw new InvalidOperationException($"Card '{card.Id}' of type '{card.Type}' must not set SourceEntityId or Evidence.");
                    }
                }
            }
            if (card.Type != "Grouping" && card.Children is { Count: > 0 })
            {
                throw new InvalidOperationException($"Card '{card.Id}' of type '{card.Type}' must not set Children.");
            }
            if (card.Type == "Grouping" && (card.Children is null || card.Children.Count < 2 || card.Children.Any(id => !cardIds.Contains(id) && !response.Cards.Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)))))
            {
                throw new InvalidOperationException($"Grouping card '{card.Id}' must reference at least 2 valid card Ids.");
            }
            if (card.SourceKnowledgeFactIds.Any(id => !factsById.ContainsKey(id)))
            {
                throw new InvalidOperationException($"Card '{card.Id}' references an unknown KnowledgeFactId.");
            }
            foreach (var factId in card.SourceKnowledgeFactIds)
            {
                var fact = factsById[factId];
                if (card.SourceEntityId is Guid entityId && !fact.RelatedEntityIds.Contains(entityId))
                {
                    throw new InvalidOperationException($"Card '{card.Id}' uses KnowledgeFact '{factId}' that is not related to its SourceEntityId.");
                }
            }
        }

        var groupingCards = response.Cards.Where(c => c.Type == "Grouping").ToList();
        if (groupingCards.Count > 1)
        {
            throw new InvalidOperationException("At most one Grouping card is allowed per Engram.");
        }

        // The Grouping card's actual mini-game (platform-implemented, domain-agnostic — see
        // EngramCard.RecognitionCue) needs every referenced card's own RecognitionCue audio to
        // play as the round's clue, so every referenced Id must have one set.
        var cardsById = response.Cards.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var groupingCard in groupingCards)
        {
            foreach (var childId in groupingCard.Children ?? [])
            {
                if (!cardsById.TryGetValue(childId, out var childCard) || string.IsNullOrWhiteSpace(childCard.RecognitionCue))
                {
                    throw new InvalidOperationException(
                        $"Grouping card '{groupingCard.Id}' references card '{childId}' which must have a non-empty RecognitionCue.");
                }
            }
        }

        var storyCards = response.Cards.Where(c => c.Type == "Story").ToList();
        if (storyCards.Count > 1)
        {
            throw new InvalidOperationException("At most one Story card is allowed per Engram.");
        }

        // Composed examples (2026-10-08) — never let AI-composed instances
        // outnumber (or even match, without real grounding) Studio-approved
        // ones: at most 2 total, and never more than the number of grounded
        // (non-composed) Concept cards actually backing the pattern.
        var composedCount = response.Cards.Count(c => c.IsComposedExample);
        var groundedConceptCount = response.Cards.Count(c => c.Type == "Concept" && !c.IsComposedExample);
        if (composedCount > 2 || composedCount > groundedConceptCount)
        {
            throw new InvalidOperationException(
                $"Too many composed example cards ({composedCount}) relative to grounded Concept cards ({groundedConceptCount}) — max 2, and never more than the grounded count.");
        }
    }

    /// <summary>Whitespace-collapsed, lowercased comparison form used only to
    /// check that an Evidence quote is a literal (not reworded) excerpt of
    /// SourceText — tolerant of line-break/spacing differences the model may
    /// introduce when copying, same spirit as
    /// <see cref="StructuredLearningKnowledgeFactGenerationAgent"/>'s own
    /// Evidence-is-a-literal-fragment requirement.</summary>
    private static string NormalizeForEvidenceMatch(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
