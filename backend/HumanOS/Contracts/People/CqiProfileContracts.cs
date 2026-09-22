namespace HumanOS.Contracts.People;

/// <summary>The full structured CQI ("understand how you currently think,
/// learn, communicate, make decisions, and work") report data — extracted
/// by <see cref="HumanOS.Agents.CqiReportExtractionAgent"/> from an
/// uploaded HermanScience CQI assessment PDF, and the shape returned to
/// the "CQI Workspace" frontend / stored as JSON on
/// <see cref="HumanOS.Models.People.PersonCqiProfile.ProfileJson"/>.</summary>
public sealed class CqiProfileData
{
    /// <summary>E.g. "Type 6".</summary>
    public string LeadingPatternType { get; set; } = string.Empty;

    /// <summary>E.g. "The Verifier".</summary>
    public string LeadingPatternName { get; set; } = string.Empty;

    public string ProfileSnapshot { get; set; } = string.Empty;

    public string HowTheyTendToWork { get; set; } = string.Empty;

    public string ThinkingAndDecisions { get; set; } = string.Empty;

    public string CommunicationAndCollaboration { get; set; } = string.Empty;

    public string LearningAndWorkingPreferences { get; set; } = string.Empty;

    public List<string> NaturalStrengths { get; set; } = [];

    public List<string> PatternsWorthNoticing { get; set; } = [];

    public string WorkingEffectivelyWithAi { get; set; } = string.Empty;

    public string WhatHelpsThemThrive { get; set; } = string.Empty;

    public string WhatStrainCanLookLike { get; set; } = string.Empty;

    public string GrowthDirection { get; set; } = string.Empty;

    /// <summary>E.g. "balanced Type 5/Type 7 wing influence".</summary>
    public string WingInfluence { get; set; } = string.Empty;

    // --- Anchor foundation (stable type brief) ---

    /// <summary>E.g. "Type 6: The Verifier".</summary>
    public string AnchorTypeName { get; set; } = string.Empty;

    public string AnchorDescription { get; set; } = string.Empty;

    public string WhatMayDrawAttention { get; set; } = string.Empty;

    public string HowChoicesMayTakeShape { get; set; } = string.Empty;

    public string WorkAndCommunication { get; set; } = string.Empty;

    public string WhatMaySupportBalance { get; set; } = string.Empty;

    public List<string> PotentialStrengths { get; set; } = [];

    public List<string> PatternsToWatchUnderPressure { get; set; } = [];
}

/// <summary>Response for GET/POST /people/{personId}/cqi-profile.</summary>
public sealed class CqiProfileResponse
{
    public Guid PersonId { get; set; }

    public CqiProfileData? Profile { get; set; }

    public string? SourceFileName { get; set; }

    public string? ExtractionModel { get; set; }

    public DateTime? ExtractedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
