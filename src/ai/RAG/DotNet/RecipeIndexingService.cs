using System.Diagnostics;
using System.Globalization;
using Embedding.DotNet;
using Domain.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Repository.DotNet;
using SemanticSearch.DotNet;
using VectorStores.DotNet;

namespace RAG.DotNet;

public sealed class RecipeIndexingService : IRecipeIndexingService
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");

    private readonly IRecipeIndexingProjectionRepository _projectionRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRecipeDocumentBuilder _recipeDocumentBuilder;
    private readonly IRecipeProjectionFingerprintService _fingerprintService;
    private readonly ISearchQueryNormalizer _searchQueryNormalizer;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<RecipeIndexingService> _logger;

    public RecipeIndexingService(
        IRecipeIndexingProjectionRepository projectionRepository,
        IEmbeddingService embeddingService,
        IRecipeDocumentBuilder recipeDocumentBuilder,
        IVectorStore vectorStore,
        IRecipeProjectionFingerprintService? fingerprintService = null,
        ISearchQueryNormalizer? searchQueryNormalizer = null,
        ILogger<RecipeIndexingService>? logger = null)
    {
        _projectionRepository = projectionRepository ?? throw new ArgumentNullException(nameof(projectionRepository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _recipeDocumentBuilder = recipeDocumentBuilder ?? throw new ArgumentNullException(nameof(recipeDocumentBuilder));
        _fingerprintService = fingerprintService ?? new RecipeProjectionFingerprintService();
        _searchQueryNormalizer = searchQueryNormalizer ?? new DefaultSearchQueryNormalizer();
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _logger = logger ?? NullLogger<RecipeIndexingService>.Instance;
    }

    public async Task<RecipeIndexingResult> IndexAllRecipesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("rag.index-recipes", ActivityKind.Internal);
        activity?.SetTag("indexing.provider", "chroma");

        var startedAt = Stopwatch.StartNew();
        _logger.LogInformation("Recipe indexing started");

        await _vectorStore.EnsureCollectionExistsAsync(cancellationToken);

        var projections = await _projectionRepository.GetRecipeIndexingProjectionsAsync(cancellationToken);
        activity?.SetTag("indexing.total_recipes", projections.Count);
        if (projections.Count == 0)
        {
            startedAt.Stop();
            _logger.LogInformation("Recipe indexing completed with no recipes to index");
            activity?.SetTag("indexing.indexed_recipes", 0);
            activity?.SetTag("indexing.failed_recipes", 0);
            activity?.SetTag("indexing.duration_ms", startedAt.Elapsed.TotalMilliseconds);
            return new RecipeIndexingResult(0, 0, 0, string.Empty, 0, startedAt.Elapsed.TotalMilliseconds);
        }

        var documents = new List<VectorDocument>(projections.Count);
        var metadataUpdates = new List<RecipeIndexMetadataUpsert>(projections.Count);
        var embeddingModel = string.Empty;
        var embeddingDimensions = 0;
        var failedRecipes = 0;
        var skippedRecipes = 0;

        for (var i = 0; i < projections.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projection = projections[i];
            var recipe = projection.Recipe;
            var recipeId = recipe.Id.ToString(CultureInfo.InvariantCulture);
            try
            {
                var normalizationArtifact = _searchQueryNormalizer.Normalize(BuildNormalizationSeed(recipe));
                var projectionDocument = _recipeDocumentBuilder.BuildProjection(new RecipeProjectionInput(
                    recipe,
                    normalizationArtifact.NormalizationVersion,
                    projection.LanguageCoverage,
                    projection.AuthoredSourceContributions));
                var canonicalDependencyHashes = BuildCanonicalDependencyHashes(recipe);
                var fingerprint = _fingerprintService.Compute(new RecipeProjectionFingerprintInput(
                    recipe.Id,
                    projectionDocument.ProjectionSchemaVersion,
                    projectionDocument.NormalizationVersion,
                    projectionDocument.Document,
                    projectionDocument.LanguageCoverage,
                    projection.TranslationContentHashes,
                    projection.IngredientTranslationContentHashes,
                    projection.RecipeIngredientLocalizationContentHashes,
                    canonicalDependencyHashes,
                    projectionDocument.AuthoredSourceTexts));

                var existingMetadata = projection.ExistingMetadata;
                if (existingMetadata is not null
                    && string.Equals(existingMetadata.ProjectionFingerprint, fingerprint, StringComparison.Ordinal)
                    && string.Equals(existingMetadata.ProjectionSchemaVersion, projectionDocument.ProjectionSchemaVersion, StringComparison.Ordinal))
                {
                    skippedRecipes++;
                    continue;
                }

                var metadata = BuildMetadata(recipe, projectionDocument, fingerprint);

                var embedding = await _embeddingService.GenerateEmbeddingAsync(projectionDocument.Document, cancellationToken);
                embeddingModel = embedding.Model;
                embeddingDimensions = embedding.Dimensions;

                documents.Add(new VectorDocument(
                    recipeId,
                    embedding.Embedding,
                    projectionDocument.Document,
                    metadata));

                metadataUpdates.Add(new RecipeIndexMetadataUpsert(
                    recipe.Id,
                    fingerprint,
                    projectionDocument.ProjectionSchemaVersion,
                    string.Join(",", projectionDocument.LanguageCoverage),
                    DateTime.UtcNow));
            }
            catch (Exception exception)
            {
                failedRecipes++;
                _logger.LogWarning(
                    exception,
                    "Recipe indexing failed recipeId={RecipeId} recipeName={RecipeName}",
                    recipeId,
                    recipe.Name ?? string.Empty);
            }

            if ((i + 1) % 25 == 0 || i == projections.Count - 1)
            {
                _logger.LogInformation(
                    "Recipe indexing progress processed={ProcessedCount} total={TotalCount} changed={ChangedCount} skipped={SkippedCount}",
                    i + 1,
                    projections.Count,
                    documents.Count,
                    skippedRecipes);
            }
        }

        if (documents.Count > 0)
        {
            await _vectorStore.UpsertAsync(documents, cancellationToken);
            await _projectionRepository.UpsertRecipeIndexMetadataAsync(metadataUpdates, cancellationToken);
        }

        startedAt.Stop();
        _logger.LogInformation(
            "Recipe indexing completed indexed={IndexedCount} skipped={SkippedCount} failed={FailedCount} total={TotalCount} model={EmbeddingModel} dimensions={Dimensions} durationMs={DurationMs}",
            documents.Count,
            skippedRecipes,
            failedRecipes,
            projections.Count,
            embeddingModel,
            embeddingDimensions,
            startedAt.Elapsed.TotalMilliseconds);

        activity?.SetTag("indexing.indexed_recipes", documents.Count);
        activity?.SetTag("indexing.failed_recipes", failedRecipes);
        activity?.SetTag("indexing.embedding_model", embeddingModel);
        activity?.SetTag("indexing.embedding_dimensions", embeddingDimensions);
        activity?.SetTag("indexing.duration_ms", startedAt.Elapsed.TotalMilliseconds);

        return new RecipeIndexingResult(
            projections.Count,
            documents.Count,
            failedRecipes,
            embeddingModel,
            embeddingDimensions,
            startedAt.Elapsed.TotalMilliseconds);
    }

    private static IReadOnlyDictionary<string, object?> BuildMetadata(
        Recipe recipe,
        RecipeProjection projection,
        string fingerprint)
    {
        var ingredientNames = recipe.RecipeIngredients
            .Select(recipeIngredient => recipeIngredient.Ingredient?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new Dictionary<string, object?>
        {
            ["recipeId"] = recipe.Id,
            ["title"] = recipe.Name ?? string.Empty,
            ["tags"] = recipe.Tags ?? string.Empty,
            ["sourcePath"] = recipe.SourcePath ?? string.Empty,
            ["servings"] = recipe.Servings,
            ["timeToPrepareMinutes"] = recipe.TimeToPrepare,
            ["ingredientNames"] = ingredientNames,
            ["hasFreezingNotes"] = !string.IsNullOrWhiteSpace(recipe.FreezingNotes),
            ["hasReheatNotes"] = !string.IsNullOrWhiteSpace(recipe.ReheatNotes),
            ["projectionSchemaVersion"] = projection.ProjectionSchemaVersion,
            ["projectionFingerprint"] = fingerprint,
            ["normalizationVersion"] = projection.NormalizationVersion,
            ["languageCoverage"] = projection.LanguageCoverage
        };
    }

    private static string BuildNormalizationSeed(Recipe recipe)
    {
        var ingredientNames = recipe.RecipeIngredients
            .Select(ingredient => ingredient.Ingredient?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name));

        return string.Join(' ', new[]
        {
            recipe.Name,
            recipe.Tags,
            recipe.Notes,
            recipe.Prepping,
            string.Join(' ', ingredientNames)
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static IReadOnlyList<string> BuildCanonicalDependencyHashes(Recipe recipe)
    {
        var values = new List<string>
        {
            Hash($"recipe:{recipe.Id}"),
            Hash($"name:{recipe.Name}"),
            Hash($"tags:{recipe.Tags}"),
            Hash($"notes:{recipe.Notes}"),
            Hash($"prepping:{recipe.Prepping}"),
            Hash($"servings:{recipe.Servings?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}"),
            Hash($"time:{recipe.TimeToPrepare?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}")
        };

        values.AddRange(recipe.RecipeIngredients
            .OrderBy(ingredient => ingredient.IngredientId)
            .Select(ingredient => Hash($"ingredient:{ingredient.IngredientId}:{ingredient.Amount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}:{ingredient.Unit}:{ingredient.Ingredient?.Name}")));

        return values
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Hash(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
