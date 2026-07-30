namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// One "example card" shown during the OPTIONAL Examples step (present only
/// on capabilities with
/// <see cref="HumanOS.Models.Capabilities.Capability.IncludeExamplesStep"/> =
/// true — see <see cref="ExperienceStepType.Examples"/>). Stored as a JSON
/// array in <see cref="NodeExperienceBlueprintStep.ExampleCardsJson"/>, never
/// as its own DB table — same lightweight "metadata-as-JSON-blob" convention
/// already used for CapabilityGraphNode.ExamplesJson/ApplicationsJson.
///
/// Illustrated with a plain emoji string (zero-cost, zero-latency — same
/// convention already proven by EngramReviewAgent's `show_visual_aid`
/// "emoji" kind), never a generated image. A future paid "real generated
/// illustration per card" option can be added later without another schema
/// migration by adding a nullable IllustrationId here.
/// </summary>
public sealed class ExampleCard
{
    /// <summary>Short card title, e.g. "Letter Aa" or "Rhyming Words".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Card body — sanitized HTML, same restricted tag set as
    /// NodeExperienceBlueprintStep.Content (p, br, strong, em, ul, ol, li, a).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Emoji(s) illustrating this card, e.g. "🍎🐜🐊". Optional —
    /// a card may have none if no emoji genuinely fits the concept. Still
    /// used as a lightweight fallback when <see cref="IllustrationId"/> is
    /// null (e.g. image generation failed or isn't configured).</summary>
    public string? Emoji { get; set; }

    /// <summary>
    /// True when ExperienceDesignerAgent judged this specific card worth a
    /// real generated illustration (2026-07-29 — see the "COVERAGE POOL"
    /// rules in <see cref="HumanOS.Agents.Studio.ExperienceDesignerAgent"/>'s
    /// Examples-step instructions). Only cards with this true are ever sent
    /// to <see cref="Services.ExampleCardIllustrationService"/> (itself also
    /// capped, as a cost safety net) — every other card teaches via
    /// Title/Text/Emoji alone.
    /// </summary>
    public bool SuggestedForIllustration { get; set; }

    /// <summary>
    /// Optional real generated illustration for this card (2026-07-29 —
    /// see <see cref="Services.ExampleCardIllustrationService"/>). Null
    /// when generation failed, isn't configured, or hasn't run yet — the
    /// UI falls back to <see cref="Emoji"/> in that case. References a
    /// CapabilityGraphNodeIllustration row (Purpose=Example); fetch bytes
    /// via GET /api/illustrations/{IllustrationId}/image, same as every
    /// other illustration.
    /// </summary>
    public Guid? IllustrationId { get; set; }
}
