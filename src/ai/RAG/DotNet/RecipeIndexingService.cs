using System.Diagnostics;
using System.Globalization;
using Embedding.DotNet;
using Domain.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Repository.DotNet;
using VectorStores.DotNet;

namespace RAG.DotNet;

public sealed class RecipeIndexingService : IRecipeIndexingService
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");

    private readonly IRecipeRepository _recipeRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRecipeDocumentBuilder _recipeDocumentBuilder;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<RecipeIndexingService> _logger;

    public RecipeIndexingService(
        IRecipeRepository recipeRepository,
        IEmbeddingService embeddingService,
        IRecipeDocumentBuilder recipeDocumentBuilder,
        IVectorStore vectorStore,
        ILogger<RecipeIndexingService>? logger = null)
    {
        _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _recipeDocumentBuilder = recipeDocumentBuilder ?? throw new ArgumentNullException(nameof(recipeDocumentBuilder));
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

        var recipes = (await _recipeRepository.GetRecipesAsync()).ToList();
        activity?.SetTag("indexing.total_recipes", recipes.Count);
        if (recipes.Count == 0)
        {
            startedAt.Stop();
            _logger.LogInformation("Recipe indexing completed with no recipes to index");
            activity?.SetTag("indexing.indexed_recipes", 0);
            activity?.SetTag("indexing.failed_recipes", 0);
            activity?.SetTag("indexing.duration_ms", startedAt.Elapsed.TotalMilliseconds);
            return new RecipeIndexingResult(0, 0, 0, string.Empty, 0, startedAt.Elapsed.TotalMilliseconds);
        }

        var documents = new List<VectorDocument>(recipes.Count);
        var embeddingModel = string.Empty;
        var embeddingDimensions = 0;
        var failedRecipes = 0;

        for (var i = 0; i < recipes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recipe = recipes[i];
            var recipeId = recipe.Id.ToString(CultureInfo.InvariantCulture);
            try
            {
                var semanticDocument = _recipeDocumentBuilder.Build(recipe);
                var metadata = BuildMetadata(recipe);

                var embedding = await _embeddingService.GenerateEmbeddingAsync(semanticDocument, cancellationToken);
                embeddingModel = embedding.Model;
                embeddingDimensions = embedding.Dimensions;

                documents.Add(new VectorDocument(
                    recipeId,
                    embedding.Embedding,
                    semanticDocument,
                    metadata));
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

            if ((i + 1) % 25 == 0 || i == recipes.Count - 1)
            {
                _logger.LogInformation(
                    "Recipe indexing progress indexed={IndexedCount} total={TotalCount}",
                    i + 1,
                    recipes.Count);
            }
        }

        if (documents.Count > 0)
        {
            await _vectorStore.UpsertAsync(documents, cancellationToken);
        }

        startedAt.Stop();
        _logger.LogInformation(
            "Recipe indexing completed indexed={IndexedCount} failed={FailedCount} total={TotalCount} model={EmbeddingModel} dimensions={Dimensions} durationMs={DurationMs}",
            documents.Count,
            failedRecipes,
            recipes.Count,
            embeddingModel,
            embeddingDimensions,
            startedAt.Elapsed.TotalMilliseconds);

        activity?.SetTag("indexing.indexed_recipes", documents.Count);
        activity?.SetTag("indexing.failed_recipes", failedRecipes);
        activity?.SetTag("indexing.embedding_model", embeddingModel);
        activity?.SetTag("indexing.embedding_dimensions", embeddingDimensions);
        activity?.SetTag("indexing.duration_ms", startedAt.Elapsed.TotalMilliseconds);

        return new RecipeIndexingResult(
            recipes.Count,
            documents.Count,
            failedRecipes,
            embeddingModel,
            embeddingDimensions,
            startedAt.Elapsed.TotalMilliseconds);
    }

    private static IReadOnlyDictionary<string, object?> BuildMetadata(Recipe recipe)
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
            ["hasReheatNotes"] = !string.IsNullOrWhiteSpace(recipe.ReheatNotes)
        };
    }
}
