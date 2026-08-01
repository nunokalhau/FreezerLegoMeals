using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace RAG.DotNet;

public sealed class RecipeStartupIndexingHostedService : IHostedService
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecipeStartupIndexingOptions _options;
    private readonly ILogger<RecipeStartupIndexingHostedService> _logger;

    public RecipeStartupIndexingHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RecipeStartupIndexingOptions> options,
        ILogger<RecipeStartupIndexingHostedService>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<RecipeStartupIndexingHostedService>.Instance;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Startup recipe indexing disabled");
            return;
        }

        var timeout = _options.StartupTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : _options.StartupTimeout;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var token = linkedCts.Token;

        using var activity = ActivitySource.StartActivity("rag.startup-indexing", ActivityKind.Internal);
        activity?.SetTag("indexing.startup.enabled", true);
        activity?.SetTag("indexing.startup.timeout_ms", timeout.TotalMilliseconds);

        var startedAt = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Startup recipe indexing enabled timeoutMs={TimeoutMs}",
                timeout.TotalMilliseconds);

            using var scope = _scopeFactory.CreateScope();
            var recipeIndexingService = scope.ServiceProvider.GetRequiredService<IRecipeIndexingService>();

            var result = await recipeIndexingService.IndexAllRecipesAsync(token);
            startedAt.Stop();

            _logger.LogInformation(
                "Startup recipe indexing completed total={TotalRecipes} indexed={IndexedRecipes} failures={FailedRecipes} model={EmbeddingModel} elapsedMs={ElapsedMs}",
                result.TotalRecipes,
                result.IndexedRecipes,
                result.FailedRecipes,
                result.EmbeddingModel,
                startedAt.Elapsed.TotalMilliseconds);

            activity?.SetTag("indexing.startup.total_recipes", result.TotalRecipes);
            activity?.SetTag("indexing.startup.indexed_recipes", result.IndexedRecipes);
            activity?.SetTag("indexing.startup.failed_recipes", result.FailedRecipes);
            activity?.SetTag("indexing.startup.embedding_model", result.EmbeddingModel);
            activity?.SetTag("indexing.startup.elapsed_ms", startedAt.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            startedAt.Stop();
            _logger.LogWarning(
                "Startup recipe indexing timed out timeoutMs={TimeoutMs} elapsedMs={ElapsedMs}",
                timeout.TotalMilliseconds,
                startedAt.Elapsed.TotalMilliseconds);
            activity?.SetTag("indexing.startup.timeout", true);
            activity?.SetTag("indexing.startup.elapsed_ms", startedAt.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            startedAt.Stop();
            _logger.LogWarning(
                exception,
                "Startup recipe indexing failed elapsedMs={ElapsedMs}",
                startedAt.Elapsed.TotalMilliseconds);
            activity?.SetTag("indexing.startup.failed", true);
            activity?.SetTag("indexing.startup.elapsed_ms", startedAt.Elapsed.TotalMilliseconds);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
