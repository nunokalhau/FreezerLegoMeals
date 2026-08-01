namespace RAG.DotNet;

public sealed record RecipeIndexingResult(
    int TotalRecipes,
    int IndexedRecipes,
    int FailedRecipes,
    string EmbeddingModel,
    int EmbeddingDimensions,
    double DurationMs);
