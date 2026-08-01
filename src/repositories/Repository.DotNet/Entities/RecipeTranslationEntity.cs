namespace Repository.DotNet.Entities;

public class RecipeTranslationEntity
{
    public int RecipeId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Tags { get; set; }

    public string? Notes { get; set; }

    public string? Prepping { get; set; }

    public int TranslationVersion { get; set; } = 1;

    public string ContentHash { get; set; } = string.Empty;

    public string Provenance { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }

    public RecipeEntity Recipe { get; set; } = null!;
}
