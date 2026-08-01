namespace WebApi.DotNet.Contracts.Responses;

public sealed class AdminReindexResponse
{
    public int RecipesIndexed { get; set; }

    public int TotalRecipes { get; set; }

    public int Failures { get; set; }

    public double ElapsedMs { get; set; }

    public string EmbeddingModel { get; set; } = string.Empty;

    public string CollectionName { get; set; } = string.Empty;
}
