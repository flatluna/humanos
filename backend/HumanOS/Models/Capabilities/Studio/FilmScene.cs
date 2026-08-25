namespace HumanOS.Models.Capabilities.Studio;

/// <summary>How the scene's anchor image was obtained — chosen by the
/// user per scene in the wizard.</summary>
public enum FilmSceneAnchorImageMode
{
    /// <summary>User uploaded a real photo to use as the subject reference.</summary>
    Uploaded = 0,

    /// <summary>No upload — the image is generated from scratch using the
    /// same scene prompt produced by <see cref="HumanOS.Agents.Studio.FilmSceneContentAgent"/>.</summary>
    GeneratedFromScenePrompt = 1,

    /// <summary>No upload — the image is generated from scratch using a
    /// SEPARATE prompt the user wrote just for the image (independent of
    /// the scene's narrative prompt).</summary>
    GeneratedFromCustomPrompt = 2,

    /// <summary>Auto-storyboard only (2026-08-24): no upload — the PREVIOUS
    /// scene's final image is used as the subject reference so the same
    /// character/framing carries across scenes instead of each one being
    /// generated independently from scratch. The very first scene either
    /// uses an optional user-uploaded anchor photo (see
    /// FilmProductionOrchestrator.ProcessScriptAsync) or falls back to
    /// GeneratedFromScenePrompt if none was provided.</summary>
    ChainedFromPreviousScene = 3,
}

public enum FilmSceneStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}

/// <summary>
/// One scene in a <see cref="FilmProduction"/> — the user's raw
/// screenplay-style input (description, dialogue, characters, camera
/// movement, environment) plus the two agents' output (final prompt +
/// composited image). One row per wizard loop iteration.
/// </summary>
public sealed class FilmScene
{
    public Guid FilmSceneId { get; set; } = Guid.NewGuid();

    public Guid FilmProductionId { get; set; }

    public int SortOrder { get; set; }

    // --- Raw user input (Paso 2 del wizard) ---

    public string Description { get; set; } = string.Empty;

    /// <summary>What is said/narrated in this scene (dialogue/voiceover).</summary>
    public string? DialogueText { get; set; }

    public string? Characters { get; set; }

    /// <summary>Camera movements / shot directions (e.g. "slow zoom in, pan left").</summary>
    public string? CameraMovements { get; set; }

    /// <summary>Free-text description of the environment/background the
    /// anchor image should be placed into.</summary>
    public string Environment { get; set; } = string.Empty;

    public FilmSceneAnchorImageMode AnchorImageMode { get; set; }

    /// <summary>Only set when AnchorImageMode == GeneratedFromCustomPrompt —
    /// the user's own prompt used INSTEAD of the scene's narrative prompt
    /// to generate the image from scratch.</summary>
    public string? CustomImagePrompt { get; set; }

    /// <summary>Blob path of the raw uploaded anchor image, only set when
    /// AnchorImageMode == Uploaded.</summary>
    public string? AnchorImageStoragePath { get; set; }

    // --- Agent A output: content review + prompt ---

    public string? SuggestedSceneName { get; set; }

    public string? GeneratedPrompt { get; set; }

    /// <summary>Agent A (FilmSceneContentAgent) token usage for this scene
    /// (2026-08-24) — feeds the production-level cost summary, see
    /// <see cref="HumanOS.Services.TokenCostEstimator"/>.</summary>
    public int ContentInputTokens { get; set; }

    public int ContentOutputTokens { get; set; }

    public int ContentCachedInputTokens { get; set; }

    public string? ContentModelName { get; set; }

    // --- Agent B output: final composited image ---

    public string? FinalImageStoragePath { get; set; }

    public string? ImageModel { get; set; }

    // --- Phase 2: per-scene video (2026-08-24) ---

    /// <summary>Blob path of the generated 10-second video clip for this
    /// scene (Gemini Omni Flash, image-to-video using FinalImageStoragePath
    /// as the reference frame + GeneratedPrompt).</summary>
    public string? FinalVideoStoragePath { get; set; }

    public string? VideoModel { get; set; }

    public FilmSceneStatus VideoStatus { get; set; } = FilmSceneStatus.Pending;

    public string? VideoErrorMessage { get; set; }

    public FilmSceneStatus Status { get; set; } = FilmSceneStatus.Pending;

    public string? ErrorMessage { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
