namespace Repository.DotNet.Entities;

public class RecipeIngredientLocalizationEntity
{
    public int RecipeId { get; set; }

    public int IngredientId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string? AmountText { get; set; }

    public string? UnitText { get; set; }

    public string? Notes { get; set; }

    public string? SourceText { get; set; }

    public int TranslationVersion { get; set; } = 1;

    public string ContentHash { get; set; } = string.Empty;

    public string Provenance { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }

    public RecipeIngredientEntity RecipeIngredient { get; set; } = null!;
}
