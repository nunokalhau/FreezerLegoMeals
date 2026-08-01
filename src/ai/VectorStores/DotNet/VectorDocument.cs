namespace VectorStores.DotNet;

public sealed record VectorDocument(
    string RecipeId,
    IReadOnlyList<float> Embedding,
    string? Document = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
