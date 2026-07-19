namespace RAG.DotNet;

public sealed record SourceAttribution(string RecipeId, string Title, double SimilarityScore);

public sealed record RetrievalRecipe(
    string RecipeId,
    string Title,
    string Description,
    string Tags,
    IReadOnlyList<string> Ingredients,
    string PreparationSteps,
    string CookingTime,
    double SimilarityScore);

public sealed record RetrievalResult(string Question, IReadOnlyList<RetrievalRecipe> Recipes, IReadOnlyList<SourceAttribution> Sources);