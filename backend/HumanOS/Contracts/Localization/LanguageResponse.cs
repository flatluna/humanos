namespace HumanOS.Contracts.Localization;

public sealed class LanguageResponse
{
    public string LanguageCode { get; set; } = null!;

    public string EnglishName { get; set; } = null!;

    public string NativeName { get; set; } = null!;

    public bool IsActive { get; set; }
}
