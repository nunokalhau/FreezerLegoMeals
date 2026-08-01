using Domain.DotNet;

namespace RAG.DotNet;

public sealed record RecipeProjectionInput(
    Recipe Recipe,
    string NormalizationVersion,
    IReadOnlyList<string>? LanguageCoverage = null,
    IReadOnlyList<string>? AuthoredSourceTexts = null,
    string? ProjectionSchemaVersion = null);

public sealed record RecipeProjection(
    string Document,
    string ProjectionSchemaVersion,
    string NormalizationVersion,
    IReadOnlyList<string> LanguageCoverage,
    IReadOnlyList<string> AuthoredSourceTexts);

public sealed record RecipeProjectionFingerprintInput(
    int RecipeId,
    string ProjectionSchemaVersion,
    string NormalizationVersion,
    string ProjectionDocument,
    IReadOnlyList<string> LanguageCoverage,
    IReadOnlyList<string> TranslationContentHashes,
    IReadOnlyList<string> IngredientTranslationContentHashes,
    IReadOnlyList<string> RecipeIngredientLocalizationContentHashes,
    IReadOnlyList<string> CanonicalDependencyHashes,
    IReadOnlyList<string> AuthoredSourceContributions);
