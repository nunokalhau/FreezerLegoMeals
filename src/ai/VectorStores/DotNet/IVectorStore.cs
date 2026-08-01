namespace VectorStores.DotNet;

public interface IVectorStore
{
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(IReadOnlyList<VectorDocument> documents, CancellationToken cancellationToken = default);

    Task DeleteAsync(IReadOnlyList<string> recipeIds, CancellationToken cancellationToken = default);

    Task ClearCollectionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default);
}