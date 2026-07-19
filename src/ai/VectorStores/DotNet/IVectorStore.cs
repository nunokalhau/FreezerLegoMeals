namespace VectorStores.DotNet;

public interface IVectorStore
{
    Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default);
}