namespace Repository.DotNet.Entities;

public class RecipeCombinationTranslationEntity
{
    public int CombinationId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public int TranslationVersion { get; set; } = 1;

    public string ContentHash { get; set; } = string.Empty;

    public string Provenance { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }

    public RecipeCombinationEntity RecipeCombination { get; set; } = null!;
}
