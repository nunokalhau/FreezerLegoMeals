namespace RAG.DotNet;

public interface IKeywordSearchService
{
    Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(string query, int topK, CancellationToken cancellationToken = default);
}