namespace RAG.DotNet;

public sealed record SourceAttribution(
    string RecipeId,
    string Title,
    double SimilarityScore,
    string CanonicalRecipeId = "",
    string RetrievalProfileId = "");

public sealed record RetrievalRecipe(
    string RecipeId,
    string CanonicalRecipeId,
    string Title,
    string Description,
    string Tags,
    IReadOnlyList<string> Ingredients,
    string PreparationSteps,
    string CookingTime,
    double SimilarityScore,
    string RetrievalProfileId = "",
    string ProjectionSchemaVersion = "",
    string NormalizationVersion = "",
    string ProjectionFingerprint = "",
    string LanguageCoverage = "");

public sealed record RetrievalResult(
    string Question,
    IReadOnlyList<RetrievalRecipe> Recipes,
    IReadOnlyList<SourceAttribution> Sources,
    RetrievalProfileDescriptor? Profile = null,
    string NormalizationVersion = "");