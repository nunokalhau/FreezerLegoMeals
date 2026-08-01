namespace RAG.DotNet;

public interface IReranker
{
    Task<IReadOnlyList<RetrievalRecipe>> RerankAsync(
        string query,
        IReadOnlyList<RetrievalRecipe> candidates,
        CancellationToken cancellationToken = default);
}