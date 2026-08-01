namespace SemanticSearch.DotNet;

public sealed record RecipeMetadata(
	string RecipeId,
	string Title,
	string MatchedText,
	string Description = "",
	string Tags = "",
	IReadOnlyList<string>? IngredientNames = null,
	string PreparationSteps = "",
	string CookingTime = "",
	string ProjectionSchemaVersion = "",
	string NormalizationVersion = "",
	string ProjectionFingerprint = "",
	string LanguageCoverage = "")
{
	public IReadOnlyList<string> Ingredients { get; init; } = IngredientNames ?? [];
}