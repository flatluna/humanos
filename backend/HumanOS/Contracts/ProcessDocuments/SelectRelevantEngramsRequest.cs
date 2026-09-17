using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Optional request body for POST /api/process-documents/{documentId}/steps/{stepId}/relevant-engrams.
/// If provided, candidateClassIds are verified against Step 3 candidate results.
/// </summary>
public sealed class SelectRelevantEngramsRequest
{
    [JsonPropertyName("candidateClassIds")]
    public List<string>? CandidateClassIds { get; set; }

    [JsonPropertyName("requirements")]
    public WorkStepRequirementsAnalysisResult? Requirements { get; set; }

    [JsonPropertyName("candidateClasses")]
    public List<CandidateClassDto>? CandidateClasses { get; set; }
}
