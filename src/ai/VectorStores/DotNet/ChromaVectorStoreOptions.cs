namespace VectorStores.DotNet;

public sealed class ChromaVectorStoreOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8001";

    public string Tenant { get; set; } = "default_tenant";

    public string Database { get; set; } = "default_database";

    public string CollectionName { get; set; } = "recipe_embeddings";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}