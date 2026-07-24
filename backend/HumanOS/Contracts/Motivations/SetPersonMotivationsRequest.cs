namespace HumanOS.Contracts.Motivations;

/// <summary>Replaces the full set of a person's Motivations with the given
/// catalog Codes (e.g. "curiosity", "growth"). Unknown codes are ignored.</summary>
public sealed class SetPersonMotivationsRequest
{
    public List<string> MotivationCodes { get; set; } = [];
}
