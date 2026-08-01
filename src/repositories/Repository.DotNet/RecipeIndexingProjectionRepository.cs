using Domain.DotNet;

namespace Repository.DotNet;

public interface IRecipeIndexingProjectionRepository
{
    Task<IReadOnlyList<RecipeIndexingProjection>> GetRecipeIndexingProjectionsAsync(CancellationToken cancellationToken = default);

    Task UpsertRecipeIndexMetadataAsync(
        IReadOnlyList<RecipeIndexMetadataUpsert> updates,
        CancellationToken cancellationToken = default);
}

public sealed record RecipeIndexMetadataSnapshot(
    string ProjectionFingerprint,
    string ProjectionSchemaVersion,
    string LanguageCoverage,
    DateTime ProjectionGeneratedAtUtc);

public sealed record RecipeIndexingProjection(
    Recipe Recipe,
    IReadOnlyList<string> LanguageCoverage,
    IReadOnlyList<string> TranslationContentHashes,
    IReadOnlyList<string> IngredientTranslationContentHashes,
    IReadOnlyList<string> RecipeIngredientLocalizationContentHashes,
    IReadOnlyList<string> AuthoredSourceContributions,
    RecipeIndexMetadataSnapshot? ExistingMetadata);

public sealed record RecipeIndexMetadataUpsert(
    int RecipeId,
    string ProjectionFingerprint,
    string ProjectionSchemaVersion,
    string LanguageCoverage,
    DateTime ProjectionGeneratedAtUtc);
