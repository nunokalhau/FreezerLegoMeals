namespace Repository.DotNet.Entities;

public class UnitTranslationEntity
{
    public string UnitKey { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int TranslationVersion { get; set; } = 1;

    public string ContentHash { get; set; } = string.Empty;

    public string Provenance { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
}
