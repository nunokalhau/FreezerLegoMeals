using Services.DotNet;
using Repository.DotNet;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register repositories and services
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();

builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IShoppingService, ShoppingService>();
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection("Assistant"));
builder.Services.Configure<ConversationStoreOptions>(builder.Configuration.GetSection("ConversationStore"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.Timeout;
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