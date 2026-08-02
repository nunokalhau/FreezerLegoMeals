namespace RAG.DotNet;

/// <summary>
/// Attribution for a retrieved recipe source used in assistant responses.
/// </summary>
/// <param name="RecipeId">Recipe identifier used by retrieval.</param>
/// <param name="Title">Resolved recipe title.</param>
/// <param name="RetrievalScore">
/// Final retrieval utility score produced by the active profile.
/// This value is profile-dependent (for example, semantic similarity or rank-fusion utility),
/// where higher is better and primarily intended for diagnostics/ranking explainability.
/// </param>
/// <param name="CanonicalRecipeId">Canonical recipe identifier after projection resolution.</param>
/// <param name="RetrievalProfileId">Identifier of the retrieval profile that produced the source.</param>
public sealed record SourceAttribution(
    string RecipeId,
    string Title,
    double RetrievalScore,
    string CanonicalRecipeId = "",
    string RetrievalProfileId = "");

/// <summary>
/// Structured retrieval candidate that carries stage-specific scores.
/// </summary>
/// <param name="RecipeId">Recipe identifier used by retrieval.</param>
/// <param name="CanonicalRecipeId">Canonical recipe identifier after projection resolution.</param>
/// <param name="Title">Resolved recipe title.</param>
/// <param name="Description">Resolved description.</param>
/// <param name="Tags">Resolved tags.</param>
/// <param name="Ingredients">Resolved ingredient names.</param>
/// <param name="PreparationSteps">Resolved preparation steps.</param>
/// <param name="CookingTime">Resolved cooking time.</param>
/// <param name="FusionScore">
/// Final score produced by the retrieval fusion stage.
/// Higher is better. In some profiles this may be true semantic similarity,
/// while in others it is rank-fusion utility intended for ranking.
/// </param>
/// <param name="RetrievalProfileId">Identifier of the retrieval profile that produced this candidate.</param>
/// <param name="ProjectionSchemaVersion">Projection schema version used for metadata.</param>
/// <param name="NormalizationVersion">Search normalization version associated with the query.</param>
/// <param name="ProjectionFingerprint">Projection fingerprint used for traceability.</param>
/// <param name="LanguageCoverage">Language coverage metadata for the projection.</param>
/// <param name="SemanticScore">
/// Semantic score emitted by vector search before fusion.
/// Typically cosine similarity in [-1, 1], higher is better, used for ranking and diagnostics.
/// </param>
/// <param name="KeywordScore">
/// Keyword overlap score emitted by lexical search before fusion.
/// Non-normalized overlap utility where higher is better, used for fusion and diagnostics.
/// </param>
/// <param name="RerankerScore">
/// Optional reranker-provided numeric score. Null when reranker provides only ordering.
/// Intended for diagnostics and ranking explainability.
/// </param>
/// <param name="FinalRank">
/// Final 1-based rank after reranking/reordering.
/// Lower is better. Intended for diagnostics and ranking explainability.
/// </param>
public sealed record RetrievalRecipe(
    string RecipeId,
    string CanonicalRecipeId,
    string Title,
    string Description,
    string Tags,
    IReadOnlyList<string> Ingredients,
    string PreparationSteps,
    string CookingTime,
    double FusionScore,
    string RetrievalProfileId = "",
    string ProjectionSchemaVersion = "",
    string NormalizationVersion = "",
    string ProjectionFingerprint = "",
    string LanguageCoverage = "",
    double? SemanticScore = null,
    double? KeywordScore = null,
    double? RerankerScore = null,
    int FinalRank = 0);

public sealed record RetrievalResult(
    string Question,
    IReadOnlyList<RetrievalRecipe> Recipes,
    IReadOnlyList<SourceAttribution> Sources,
    RetrievalProfileDescriptor? Profile = null,
    string NormalizationVersion = "");