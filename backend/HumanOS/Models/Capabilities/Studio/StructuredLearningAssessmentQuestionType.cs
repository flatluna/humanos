using System.Text.Json.Serialization;

namespace HumanOS.Models.Capabilities.Studio;

/// <summary>User-chosen format for the Assessment step's questions
/// (2026-08-10) — Graph/Traditional today only ever generate
/// MultipleChoice; OpenEnded is a new capability this pipeline
/// introduces.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StructuredLearningAssessmentQuestionType
{
    MultipleChoice = 0,
    OpenEnded = 1
}
