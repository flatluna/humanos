using System.Text.Json.Serialization;

namespace HumanOS.Models.Capabilities.Studio;

/// <summary>Controls whether Recall/Assessment questions for a document are
/// generated once at course-creation time and reused by every student
/// ("Static" — cheaper, same questions for all learners, capped at 3 per
/// engram) or generated fresh for every student/attempt at runtime
/// ("Dynamic" — more variety, costs an LLM call per attempt). New
/// (2026-10-06) — defaults to Dynamic so existing documents keep their
/// current runtime-generation behavior unchanged.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StructuredLearningGenerationMode
{
    Dynamic = 0,
    Static = 1
}
