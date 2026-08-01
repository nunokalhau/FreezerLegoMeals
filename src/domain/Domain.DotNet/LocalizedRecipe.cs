namespace Domain.DotNet;

public sealed class LocalizedRecipe
{
    public int CanonicalRecipeId { get; init; }

    public string Language { get; init; } = string.Empty;

    public string? FallbackLanguageUsed { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Tags { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string Prepping { get; init; } = string.Empty;

    public int? TimeToPrepare { get; init; }

    public IReadOnlyList<LocalizedRecipeIngredient> Ingredients { get; init; } = Array.Empty<LocalizedRecipeIngredient>();
}
