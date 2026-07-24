namespace HumanOS.Contracts.Capabilities;

public sealed class StartCapabilityDevelopmentRequest
{
    public int TargetLevel { get; set; } = 5;

    /// <summary>
    /// Optional self-reported starting point from the individual onboarding
    /// survey ("¿Dónde estás hoy?"). One of 'Beginner', 'Intermediate',
    /// 'Advanced' (case-insensitive). When provided, seeds
    /// <see cref="HumanOS.Models.Capabilities.PersonCapability.ConfidenceScore"/>,
    /// <see cref="HumanOS.Models.Capabilities.PersonCapability.CurrentLevel"/> and
    /// <see cref="HumanOS.Models.Capabilities.PersonCapability.KnowledgeScore"/>
    /// instead of the default zero/unknown starting values. Null means the
    /// person did not self-assess (e.g. employee/organization flow).
    /// </summary>
    public string? SelfAssessedLevel { get; set; }
}
