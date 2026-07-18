using Domain.DotNet;

namespace Services.DotNet.Contracts;

public sealed class IngredientSearchResponse
{
    public string Query { get; init; }

    public IEnumerable<string> SearchTerms { get; init; }

    public int TotalRecipesFound { get; init; }

    public IEnumerable<Recipe> Recipes { get; init; }

    public string Message { get; init; }
}