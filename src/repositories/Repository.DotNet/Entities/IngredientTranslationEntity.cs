namespace Repository.DotNet.Entities;

public class IngredientTranslationEntity
{
    public int IngredientId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Unit { get; set; }

    public int TranslationVersion { get; set; } = 1;

    public string ContentHash { get; set; } = string.Empty;

    public string Provenance { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }

    public IngredientEntity Ingredient { get; set; } = null!;
}
