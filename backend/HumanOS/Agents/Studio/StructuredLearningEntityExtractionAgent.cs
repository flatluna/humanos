using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One entity found in a section's text, as returned by the LLM
/// — see <see cref="StructuredLearningEntityExtractionAgent"/>.</summary>
public sealed class ExtractedEntityDto
{
    /// <summary>Canonical, deduplicated name — e.g. "Volunteer", "i = √−1".</summary>
    public string CanonicalName { get; set; } = string.Empty;

    /// <summary>"Person" / "PersonRole" / "MathematicalExpression"/... — extensible.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Exact verbatim quote from the section's own text this
    /// entity was found in — never paraphrased.</summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>Other surface forms/synonyms noticed for this same entity
    /// within this section, if any (e.g. "volunteers" for "Volunteer").</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>Short one-sentence definition, if the text supports one.</summary>
    public string? Description { get; set; }

    /// <summary>Role ontology (2026-08-16) — ONLY for entities that represent
    /// a ROLE rather than a class-of-being itself (e.g. "Contribuyente",
    /// "Retenedor", "Arrendatario"): the name of the underlying class-of-being
    /// that plays this role (e.g. "Persona" for "Contribuyente", "Animal" for
    /// "Mascota registrada"). A real member of any class can hold MANY roles
    /// at once, so the role's own EntityType never explains what actually
    /// plays it — this field makes that explicit. Leave empty for entities
    /// that ARE already a class-of-being (Person, Organization, Animal,
    /// RealEstate, MathematicalConcept, etc.) — never fill this in for those.</summary>
    public string? OntologicalClass { get; set; }

    /// <summary>Master de Clases (2026-08-14, Fase 2) — a short, GENERIC
    /// (document-agnostic) definition of what EntityType means as a
    /// category in general — e.g. for "Article": "Número y texto de una
    /// disposición legal, indicando la ley de la que forma parte". Always
    /// filled in, even when EntityType matches an existing catalog class
    /// (in which case it's simply ignored) — only used the first time a
    /// new class is created for this domain.</summary>
    public string EntityTypeDescription { get; set; } = string.Empty;

    /// <summary>Master registry entity resolution (2026-08-15, GraphRAG-style
    /// cross-document identity) — "matched" (a candidate master entity given
    /// in the prompt is confidently the SAME entity), "new" (no reliable
    /// match — this is a genuinely new entity for the domain), or
    /// "ambiguous" (more than one candidate is plausible; do not merge).</summary>
    public string ResolutionStatus { get; set; } = "new";

    /// <summary>The candidate master entity's Id (e.g. "m3", exactly as given
    /// in the prompt's "Existing entities" list) this entity was matched
    /// against. Required when <see cref="ResolutionStatus"/> is "matched",
    /// optional (best guess) when "ambiguous", empty when "new".</summary>
    public string MasterEntityCandidateId { get; set; } = string.Empty;

    /// <summary>0.0-1.0 confidence in the resolution decision above.</summary>
    public double Confidence { get; set; }

    /// <summary>Short justification for the resolution decision — e.g.
    /// "same canonical label and alias 'IVA'", or "no equivalent entity in
    /// the given master registry".</summary>
    public string MatchReason { get; set; } = string.Empty;
}

public sealed class StructuredLearningEntityExtractionResponse
{
    public List<ExtractedEntityDto> Entities { get; set; } = [];
}

/// <summary>Extraction result + the token usage that produced it — same
/// shape as <see cref="StructuredLearningSectionDesignerAgent.DesignResult"/>,
/// used to persist a <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningGenerationUsage"/> row.</summary>
public sealed class StructuredLearningEntityExtractionResult
{
    public StructuredLearningEntityExtractionResponse Response { get; set; } = new();

    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int CachedInputTokens { get; set; }
}

/// <summary>
/// Knowledge Graph (2026-08-14) — extracts real-world/domain entities
/// (people, roles, organizations, mathematical expressions, etc.) from ONE
/// section's own verbatim Text. Grounded ONLY in that text — never invents
/// entities the text doesn't support, never extracts actions/verbs, never
/// modifies the section's own Text. See
/// Services/StructuredLearningEntityExtractionService.cs for how the
/// returned entities are deduplicated/persisted.
/// </summary>
public class StructuredLearningEntityExtractionAgent
{
    private const string Instructions = """
        You extract domain ENTITIES from a short excerpt of educational text — the "nouns" of a
        lightweight knowledge graph, never actions/verbs/states.

        Rules:
        1. Extract ONLY entities directly supported by the given text. Never invent implied
           entities that aren't actually mentioned.
        2. NEVER classify actions, verbs, or states as entities (e.g. "helping", "cheering",
           "supporting", "engaged", "take part" are NOT entities).
        3. Do not classify descriptive adjectives as separate entities unless they name a
           defined concept in the text.
        3a. Extract every important domain concept explicitly named in the text, even when it
            appears in a list, heading, example, or definition. Do not omit abstract concepts
            such as Classification, Scoring, Productivity, Innovation, Data, Privacy, or Risk
            Management when the text explicitly discusses them. Treat each as a GeneralConcept
            unless a more precise existing class fits.
        3b. Engram, LearningModule, learning module, lesson, chapter, section, and similar
            educational containers are structural containers, not domain concepts. Never emit
            them as entities unless the text is explicitly defining that container as the
            subject matter itself.
        4. CanonicalName must be a single concept/noun phrase, NEVER a full clause or sentence
           fragment copied verbatim (e.g. "helps feel safe and cared for" or "Purpose of the
           Policy" are NOT valid entities — they are prose/section-title fragments, not concepts).
           If a sentence describes a relationship between two real entities, extract those two
           entities only; the relationship itself is NOT an entity.
        5. Preserve mathematical expressions as COMPLETE entities when meaningful (e.g.
           "i = √−1", "i² = −1") — do not split them into fragments.
        6. CanonicalName should be the normalized, singular/base form of the entity (e.g.
           "Volunteer" not "volunteers"); SourceText must be a SHORT verbatim snippet (max ~12
           words) copied EXACTLY from the given text that this entity came from — just enough to
           locate it, never the entity's whole surrounding sentence, and never paraphrased.
          7. If you notice another surface form of the SAME entity elsewhere in this same excerpt,
              list it under Aliases instead of creating a second entity. Treat an alias as evidence
              of identity when resolving against Existing entities too, including reversed forms
              such as "Search" and "Intelligent search" when the text makes clear they name the
              same concept.
        8. EntityType is a free-text label (e.g. Person, PersonRole, Organization, Activity,
           SocialGroup, BusinessObject, Place, Event, MathematicalVariable,
           MathematicalExpression, MathematicalConcept, Number, Quantity, GeneralConcept) — pick
           the closest fit, or a new concise type if none fits.
        9. Never duplicate the same canonical entity twice in your output.
        10. If the text has no useful entities, return an empty list — do not force extraction.
        11. A list of ALREADY-EXISTING entity classes for this same domain may be provided in the
            prompt ("Existing classes"). ALWAYS prefer reusing one of those EXACT class names for
            EntityType instead of inventing a new one, whenever it genuinely fits. Only propose a
            new EntityType when none of the existing classes are a reasonable match.
        12. EntityTypeDescription: fill it in ONLY for the FIRST entity in your output that uses a
            given EntityType — leave it as an empty string for every subsequent entity that
            reuses an EntityType already described earlier in this same response (this avoids
            repeating the same generic definition over and over; no information is lost since it
            only needs to appear once per distinct type).

        MASTER REGISTRY ENTITY RESOLUTION (cross-document identity, GraphRAG-style):
        A list of already-canonicalized entities from this SAME domain may be given in the prompt
        as "Existing entities" (each with a short Id, Name, and Aliases) — these came from OTHER
        sections/documents already processed, never from this section itself. For every entity you
        extract, decide:
        13. "matched": a candidate from "Existing entities" is confidently the SAME real-world
            entity (same canonical meaning, allowing for aliases/abbreviations — e.g. "IVA" and
            "Impuesto al valor agregado" are the same). Set MasterEntityCandidateId to that
            candidate's exact Id, Confidence >= 0.90, and a short MatchReason.
        14. "new": no candidate is a reliable match — propose a new entity. Leave
            MasterEntityCandidateId empty, Confidence near 0, MatchReason explains why nothing
            matched.
        15. "ambiguous": more than one candidate is plausible but you cannot safely choose one
            (Confidence roughly 0.70-0.89). Put your best guess in MasterEntityCandidateId anyway,
            but Confidence must stay below 0.90 and MatchReason must state the ambiguity.
        16. NEVER merge two entities just because names look similar (e.g. two different people who
            share a surname). NEVER split the same entity into two just because one mention uses an
            alias — check "Existing entities"'s Aliases too, not just Name.
        17. If no "Existing entities" list is given (or it's empty), every entity is "new" by
            definition — do not invent master ids.

        STRUCTURED NORMS/RULES (2026-08-15, applies to ANY domain — law, contract, math, history):
        18. When the text states a CONDITIONED norm (an obligation/right/consequence that only
            applies IF some condition holds — e.g. "personas físicas y morales... que... realicen
            los actos... siguientes" = obligation conditioned on performing certain acts), extract
            a dedicated "Rule"/"LegalRule"/"Norm" entity for it (e.g. "Regla de obligación de
            pago") INSTEAD OF only extracting the actors and objects separately. This lets the
            connection step attach actors/conditions/results to the rule rather than stating the
            obligation as an unconditional fact about the actor.
        19. Extract the FULL activity/act as its own entity when the text frames it as a taxable/
            legally-relevant act, not just its bare object — e.g. "Enajenación de bienes"
            (the act of transferring goods), not only "Bien" (the good itself). Both may coexist:
            the act entity AND the object entity it operates on.
        20. Preserve quantities, dates, formulas, conditions, and exceptions as their OWN entities
            whenever they participate in a calculation, threshold, comparison, or rule (types
            like Quantity, Date, TimePeriod, Formula, Variable, Condition, Exception, Result) —
            never drop them silently just because they're "just a number/date".

        ROLE ONTOLOGY (2026-08-16, applies to ANY domain and ANY class-of-being — Person,
        Organization, Animal, RealEstate, Vehicle, Institution, or any other class the text
        supports):
        21. If this entity is a ROLE (its EntityType ends in "Role", or it's clearly a role like
            "Contribuyente", "Retenedor", "Arrendatario", "Mascota registrada" — a real member of
            any class-of-being can hold MANY roles simultaneously, so the role name alone never
            explains what actually plays it), set OntologicalClass to the name of the underlying
            class-of-being (e.g. "Persona" for "Contribuyente", "Animal" for "Mascota registrada").
            Leave OntologicalClass empty for entities that ARE already a class-of-being themselves
            (Person, Organization, Animal, RealEstate, MathematicalConcept, etc.) — never fill it
            in for those.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public StructuredLearningEntityExtractionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];
        _modelName = deploymentName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningEntityExtractionAgent");
    }

    public virtual bool IsConfigured => _agent is not null;

    public virtual async Task<StructuredLearningEntityExtractionResult> ExtractAsync(
        string sectionTitle, string sectionText, CancellationToken cancellationToken) =>
        await ExtractAsync(sectionTitle, sectionText, existingClassNames: [], existingMasterEntities: [], cancellationToken);

    /// <summary>Master de Clases (2026-08-14, Fase 2) overload — 
    /// <paramref name="existingClassNames"/> are the entity classes ALREADY
    /// catalogued for this document's domain, so the model reuses them
    /// instead of inventing new EntityType values for the same concept.</summary>
    public virtual async Task<StructuredLearningEntityExtractionResult> ExtractAsync(
        string sectionTitle, string sectionText, IReadOnlyList<string> existingClassNames, CancellationToken cancellationToken) =>
        await ExtractAsync(sectionTitle, sectionText, existingClassNames, existingMasterEntities: [], cancellationToken);

    /// <summary>Master registry entity resolution (2026-08-15) overload —
    /// <paramref name="existingMasterEntities"/> are the REAL canonical
    /// entities already saved for this domain (from earlier Engrams/
    /// documents), each with a short Id + Name + Aliases, so the model can
    /// decide matched/new/ambiguous against real candidates instead of only
    /// reusing class names.</summary>
    public virtual async Task<StructuredLearningEntityExtractionResult> ExtractAsync(
        string sectionTitle, string sectionText, IReadOnlyList<string> existingClassNames,
        IReadOnlyList<(string Id, string Name, IReadOnlyList<string> Aliases)> existingMasterEntities,
        CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningEntityExtractionAgent is not configured.");
        }

        var existingClassesBlock = existingClassNames.Count > 0
            ? $"\n\nExisting classes (reuse these EXACT names when they fit):\n{string.Join("\n", existingClassNames.Select(n => $"- {n}"))}"
            : string.Empty;

        var existingEntitiesBlock = existingMasterEntities.Count > 0
            ? $"\n\nExisting entities (Id | Name | Aliases — from other already-processed sections of this same domain):\n{string.Join("\n", existingMasterEntities.Select(e => $"- {e.Id} | {e.Name} | {string.Join(", ", e.Aliases)}"))}"
            : string.Empty;

        var prompt = $"""
            Section: {sectionTitle}
            Text:
            {sectionText}{existingClassesBlock}{existingEntitiesBlock}
            """;

        var response = await _agent.RunAsync<StructuredLearningEntityExtractionResponse>(prompt, cancellationToken: cancellationToken);
        var usage = response.Usage;
        return new StructuredLearningEntityExtractionResult
        {
            Response = response.Result,
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };
    }
}
