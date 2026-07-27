using Services.DotNet;
using Repository.DotNet;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Embedding.DotNet;
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

// Register repositories and services
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();

builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddSingleton<IRoutingPolicy, DefaultRoutingPolicy>();
builder.Services.Configure<ExternalDependencyResilienceOptions>(builder.Configuration.GetSection("Resilience"));
builder.Services.AddSingleton<IExternalDependencyResiliencePolicyProvider, PollyExternalDependencyResiliencePolicyProvider>();
builder.Services.AddScoped<IAgent, MealPlanningAgent>();
builder.Services.AddScoped<IAssistantOrchestrator, AssistantOrchestrator>();
builder.Services.AddSingleton<RedisMemoryProvider>();
builder.Services.AddSingleton<IConversationStore>(serviceProvider => serviceProvider.GetRequiredService<RedisMemoryProvider>());
builder.Services.AddSingleton<IMemoryProvider>(serviceProvider => serviceProvider.GetRequiredService<RedisMemoryProvider>());
builder.Services.AddSingleton<IToolRegistry>(_ => new ToolRegistry(
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "tools", "tool_registry.json"))));
builder.Services.AddScoped<IToolExecutor>(serviceProvider => new PythonToolExecutor(
    serviceProvider.GetRequiredService<IToolRegistry>(),
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "tools")),
    logger: serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PythonToolExecutor>>(),
    resiliencePolicyProvider: serviceProvider.GetRequiredService<IExternalDependencyResiliencePolicyProvider>()));
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IShoppingService, ShoppingService>();
builder.Services.AddScoped<ISemanticRecipeMetadataProvider, RepositorySemanticRecipeMetadataProvider>();
builder.Services.AddScoped<SemanticSearchService>();
builder.Services.AddScoped<IRetrievalService, RetrievalService>();
builder.Services.AddSingleton<IPromptBuilder>(_ => PromptBuilder.FromFile(
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "ai", "RAG", "prompts", "rag_prompt.txt"))));
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection("Assistant"));
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
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.Timeout;
}).AddPolicyHandler((serviceProvider, _) =>
{
    var resilienceProvider = serviceProvider.GetRequiredService<IExternalDependencyResiliencePolicyProvider>();
    return resilienceProvider.GetHttpPolicy(ExternalDependency.Ollama);
});
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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
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