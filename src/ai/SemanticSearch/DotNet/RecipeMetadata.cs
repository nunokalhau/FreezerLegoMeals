namespace SemanticSearch.DotNet;

public sealed record RecipeMetadata(
	string RecipeId,
	string Title,
	string MatchedText,
	string Description = "",
	string Tags = "",
	IReadOnlyList<string>? IngredientNames = null,
	string PreparationSteps = "",
	string CookingTime = "")
{
	public IReadOnlyList<string> Ingredients { get; init; } = IngredientNames ?? [];
}