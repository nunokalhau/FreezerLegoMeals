using Services.DotNet;
using Repository.DotNet;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Embedding.DotNet;
using Evaluation.DotNet;
using Orchestration.DotNet;
using RAG.DotNet;
using SemanticSearch.DotNet;
using VectorStores.DotNet;
using WebApi.DotNet.Services;
using AI.Memory.DotNet;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Register repositories and services
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddScoped<IRecipeIndexingProjectionRepository, RecipeRepository>();

builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddAiEvaluationFramework();
builder.Services.AddSingleton<IRoutingPolicy, DefaultRoutingPolicy>();
builder.Services.Configure<HybridIntentClassifierOptions>(builder.Configuration.GetSection("Assistant:IntentClassification:Hybrid"));
builder.Services.AddSingleton<RuleBasedIntentClassifier>();
builder.Services.AddScoped<IIntentClassifier, HybridIntentClassifier>();
builder.Services.Configure<ExternalDependencyResilienceOptions>(builder.Configuration.GetSection("Resilience"));
builder.Services.AddSingleton<IExternalDependencyResiliencePolicyProvider, PollyExternalDependencyResiliencePolicyProvider>();
builder.Services.AddSingleton<IModelCapabilitiesCache, InMemoryModelCapabilitiesCache>();
builder.Services.AddSingleton<IModelCapabilitiesProvider, OllamaModelCapabilitiesProvider>();
builder.Services.AddScoped<IAgent, MealPlanningAgent>();
builder.Services.AddScoped<AssistantOrchestrator>();
builder.Services.AddScoped<IAssistantOrchestrator>(serviceProvider => new EvaluationAssistantOrchestrator(
    serviceProvider.GetRequiredService<AssistantOrchestrator>(),
    serviceProvider.GetRequiredService<IAiEvaluationTraceContext>()));
builder.Services.AddSingleton<RedisMemoryProvider>();
builder.Services.AddSingleton<IConversationStore>(serviceProvider => serviceProvider.GetRequiredService<RedisMemoryProvider>());
builder.Services.AddSingleton<IMemoryProvider>(serviceProvider => serviceProvider.GetRequiredService<RedisMemoryProvider>());
builder.Services.AddSingleton<IToolRegistry>(_ => new ToolRegistry(
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "tools", "tool_registry.json"))));
builder.Services.AddScoped<IToolExecutor>(serviceProvider =>
{
    var pythonToolExecutor = new PythonToolExecutor(
        serviceProvider.GetRequiredService<IToolRegistry>(),
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "tools")),
        searchQueryNormalizer: serviceProvider.GetRequiredService<ISearchQueryNormalizer>(),
        logger: serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PythonToolExecutor>>(),
        resiliencePolicyProvider: serviceProvider.GetRequiredService<IExternalDependencyResiliencePolicyProvider>());

    return new EvaluationToolExecutor(
        pythonToolExecutor,
        serviceProvider.GetRequiredService<IAiEvaluationTraceContext>());
});
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IShoppingService, ShoppingService>();
builder.Services.AddSingleton<IAssistantLanguageDetector, HeuristicAssistantLanguageDetector>();
builder.Services.AddSingleton<ILanguageContextResolver, LanguageContextResolver>();
builder.Services.AddSingleton<ILocalizationOptionsFactory, LocalizationOptionsFactory>();
builder.Services.AddScoped<ILocalizedRecipeQueryService, LocalizedRecipeQueryService>();
builder.Services.Configure<ApiLocalizationOptions>(builder.Configuration.GetSection("Localization"));
builder.Services.AddScoped<IApiRecipeLocalizationService, ApiRecipeLocalizationService>();
builder.Services.AddScoped<ISemanticRecipeMetadataProvider, RepositorySemanticRecipeMetadataProvider>();
var searchNormalizationOptions = builder.Configuration.GetSection("SearchNormalization").Get<SearchNormalizationOptions>()
    ?? new SearchNormalizationOptions();
builder.Services.AddSingleton(searchNormalizationOptions);
builder.Services.AddSingleton<ISearchQueryNormalizer>(serviceProvider =>
    new DefaultSearchQueryNormalizer(serviceProvider.GetRequiredService<SearchNormalizationOptions>()));
builder.Services.AddScoped<SemanticSearchService>();
builder.Services.AddScoped<IQueryRewriter, QueryRewriterService>();
builder.Services.AddScoped<IKeywordSearchService, KeywordSearchService>();
builder.Services.AddScoped<IReranker, OllamaRerankerService>();
builder.Services.AddScoped<IAnswerGroundingService, AnswerGroundingService>();
builder.Services.AddScoped<RetrievalService>();
builder.Services.AddSingleton<IRecipeDocumentBuilder, RecipeDocumentBuilder>();
builder.Services.AddSingleton<IRecipeProjectionFingerprintService, RecipeProjectionFingerprintService>();
builder.Services.AddScoped<IRecipeIndexingService, RecipeIndexingService>();
builder.Services.Configure<RecipeStartupIndexingOptions>(builder.Configuration.GetSection("AI:Indexing"));
builder.Services.Configure<RerankingOptions>(builder.Configuration.GetSection("AI:Reranking"));
builder.Services.AddHostedService<RecipeStartupIndexingHostedService>();
builder.Services.AddScoped<IRetrievalService>(serviceProvider => new EvaluationRetrievalService(
    serviceProvider.GetRequiredService<RetrievalService>(),
    serviceProvider.GetRequiredService<IAiEvaluationTraceContext>()));
builder.Services.AddSingleton<IPromptBuilder, PromptBuilder>();
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection("Assistant"));
builder.Services.Configure<AssistantLocalizationDefaultsOptions>(builder.Configuration.GetSection("Localization"));
builder.Services.Configure<ConversationStoreOptions>(builder.Configuration.GetSection("ConversationStore"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embeddings"));
builder.Services.Configure<ChromaVectorStoreOptions>(builder.Configuration.GetSection("ChromaVectorStore"));
builder.Services.AddHttpClient(ChromaVectorStore.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChromaVectorStoreOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.Timeout;
}).AddPolicyHandler((serviceProvider, _) =>
{
    var resilienceProvider = serviceProvider.GetRequiredService<IExternalDependencyResiliencePolicyProvider>();
    return resilienceProvider.GetHttpPolicy(ExternalDependency.ChromaDb);
});
builder.Services.AddSingleton<IVectorStore>(serviceProvider => new ChromaVectorStore(
    serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(ChromaVectorStore.HttpClientName),
    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChromaVectorStoreOptions>>(),
    logger: serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ChromaVectorStore>>()));
builder.Services.AddHttpClient<OllamaClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.Timeout;
}).AddPolicyHandler((serviceProvider, _) =>
{
    var resilienceProvider = serviceProvider.GetRequiredService<IExternalDependencyResiliencePolicyProvider>();
    return resilienceProvider.GetHttpPolicy(ExternalDependency.Ollama);
});
builder.Services.AddScoped<IOllamaClient>(serviceProvider => new EvaluationOllamaClient(
    serviceProvider.GetRequiredService<OllamaClient>(),
    serviceProvider.GetRequiredService<IAiEvaluationTraceContext>()));
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmbeddingOptions>>().Value;
    client.BaseAddress = new Uri(options.OllamaBaseUrl);
    client.Timeout = options.Timeout;
}).AddPolicyHandler((serviceProvider, _) =>
{
    var resilienceProvider = serviceProvider.GetRequiredService<IExternalDependencyResiliencePolicyProvider>();
    return resilienceProvider.GetHttpPolicy(ExternalDependency.Ollama);
});

builder.Services.AddDbContext<FreezerLegoMealsContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

    var normalized = connectionString.TrimStart();
    if (normalized.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
        || normalized.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
        || normalized.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/healthz");

app.Run();

// For testing purposes
namespace WebApi.DotNet
{
    public partial class Program
    {
    }
}