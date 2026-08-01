using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAG.DotNet;
using VectorStores.DotNet;
using WebApi.DotNet.Contracts.Responses;

namespace WebApi.DotNet.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");

    private readonly IVectorStore _vectorStore;
    private readonly IRecipeIndexingService _recipeIndexingService;
    private readonly ChromaVectorStoreOptions _chromaOptions;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IVectorStore vectorStore,
        IRecipeIndexingService recipeIndexingService,
        IOptions<ChromaVectorStoreOptions> chromaOptions,
        ILogger<AdminController> logger)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _recipeIndexingService = recipeIndexingService ?? throw new ArgumentNullException(nameof(recipeIndexingService));
        _chromaOptions = chromaOptions?.Value ?? throw new ArgumentNullException(nameof(chromaOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("reindex")]
    public async Task<ActionResult<AdminReindexResponse>> Reindex(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("admin.reindex", ActivityKind.Internal);
        activity?.SetTag("admin.operation", "reindex");
        activity?.SetTag("vector_store.collection", _chromaOptions.CollectionName);

        var startedAt = Stopwatch.StartNew();
        _logger.LogInformation(
            "Admin reindex started collection={CollectionName}",
            _chromaOptions.CollectionName);

        await _vectorStore.EnsureCollectionExistsAsync(cancellationToken);
        await _vectorStore.ClearCollectionAsync(cancellationToken);
        var indexingResult = await _recipeIndexingService.IndexAllRecipesAsync(cancellationToken);

        startedAt.Stop();
        var response = new AdminReindexResponse
        {
            RecipesIndexed = indexingResult.IndexedRecipes,
            TotalRecipes = indexingResult.TotalRecipes,
            Failures = indexingResult.FailedRecipes,
            ElapsedMs = startedAt.Elapsed.TotalMilliseconds,
            EmbeddingModel = indexingResult.EmbeddingModel,
            CollectionName = _chromaOptions.CollectionName
        };

        _logger.LogInformation(
            "Admin reindex completed collection={CollectionName} total={TotalRecipes} indexed={IndexedRecipes} failures={Failures} elapsedMs={ElapsedMs} embeddingModel={EmbeddingModel}",
            response.CollectionName,
            response.TotalRecipes,
            response.RecipesIndexed,
            response.Failures,
            response.ElapsedMs,
            response.EmbeddingModel);

        activity?.SetTag("admin.reindex.total_recipes", response.TotalRecipes);
        activity?.SetTag("admin.reindex.indexed_recipes", response.RecipesIndexed);
        activity?.SetTag("admin.reindex.failures", response.Failures);
        activity?.SetTag("admin.reindex.elapsed_ms", response.ElapsedMs);
        activity?.SetTag("admin.reindex.embedding_model", response.EmbeddingModel);

        return Ok(response);
    }
}
