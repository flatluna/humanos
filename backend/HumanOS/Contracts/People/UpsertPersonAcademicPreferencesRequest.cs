namespace HumanOS.Contracts.People;

public sealed class UpsertPersonAcademicPreferencesRequest
{
    public string[] AcademicInterests { get; set; } = [];

    public string[] LearningPreferences { get; set; } = [];

    public string[] AcademicMotivations { get; set; } = [];

    public string[] GrowthAreas { get; set; } = [];
}
