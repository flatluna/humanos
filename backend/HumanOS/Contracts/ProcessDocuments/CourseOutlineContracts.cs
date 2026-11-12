using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>Request to extract or (as fallback) AI-generate a suggested module index/outline for a course,
/// so that EngramsIQ Studio can later use it as a starting draft to auto-generate the course content.</summary>
public sealed class CourseOutlineRequest
{
    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("courseTitle")]
    public string CourseTitle { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("processName")]
    public string? ProcessName { get; set; }
}

public sealed class CourseOutlineResult
{
    [JsonPropertyName("courseTitle")]
    public string CourseTitle { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>"scraped" when the modules below were extracted from the real course page, or
    /// "ai-suggested" when no usable index could be scraped and the LLM generated a plausible draft instead.</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "ai-suggested";

    [JsonPropertyName("modules")]
    public List<CourseOutlineModule> Modules { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

public sealed class CourseOutlineModule
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("lessons")]
    public List<string> Lessons { get; set; } = [];
}
