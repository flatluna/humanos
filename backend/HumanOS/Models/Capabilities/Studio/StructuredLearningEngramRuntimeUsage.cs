namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One real student runtime interaction with an engram's "Q&A with
/// Professor" (2026-10-01) — the per-use (NOT per-authoring) cost
/// ledger: a Realtime voice turn (duration reported by the browser, see
/// <see cref="HumanOS.AzureFunctions.Api.PostStructuredLearningEngramVoiceUsageFunction"/>,
/// same client-reports pattern as BuddySessionUsage) or one text Q&A
/// chat turn (tokens read straight from the Agent's own response.Usage,
/// see StructuredLearningQaProfessorChatService). One row per turn — lets
/// cost be rolled up by Person/Tenant/Document(course)/Section(engram).
/// </summary>
public sealed class StructuredLearningEngramRuntimeUsage
{
    public Guid StructuredLearningEngramRuntimeUsageId { get; set; } = Guid.NewGuid();

    /// <summary>FK: the student who used the Professor. Null if the caller
    /// couldn't be resolved — excluded from any per-tenant/per-person rollup.</summary>
    public Guid? PersonId { get; set; }

    /// <summary>The course this engram belongs to.</summary>
    public Guid? StructuredLearningDocumentId { get; set; }

    /// <summary>The engram (section) the student was asking about.</summary>
    public Guid? StructuredLearningDocumentSectionId { get; set; }

    /// <summary>The real visit (StructuredLearningEngramSession) this usage
    /// happened during (2026-10-02) — lets cost be rolled up per real
    /// session, not just lifetime-per-person. Null for usage recorded
    /// before this FK existed.</summary>
    public Guid? StructuredLearningEngramSessionId { get; set; }

    /// <summary>"Voice", "Text", or "TextToSpeech".</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>"Hypothesis"/"Teaching"/"DeepDive"/"Production"/"Recall"/"Assessment"
    /// (2026-10-02) — which of the 6 pedagogy steps this usage happened in,
    /// so cost/tokens can be rolled up per step, not just per channel. Null
    /// for rows recorded before this column existed.</summary>
    public string? StepType { get; set; }

    /// <summary>Azure OpenAI deployment that served this turn (e.g.
    /// "gpt-realtime-mini" for Voice, "gpt-5-mini" for Text).</summary>
    public string? ModelName { get; set; }

    /// <summary>Input to the model, including context; from text response usage
    /// or Realtime response.done usage, not only the student's audio.</summary>
    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    /// <summary>Characters billed by Azure Speech for TextToSpeech rows.</summary>
    public int CharacterCount { get; set; }

    /// <summary>Voice channel only — wall-clock call duration measured
    /// client-side from WebRTC connect to teardown, same pattern as
    /// BuddySessionUsage (the backend never sees the live audio stream).</summary>
    public int DurationSeconds { get; set; }

    public DateTime CreatedDate { get; set; }
}
