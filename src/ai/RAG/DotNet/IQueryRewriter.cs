namespace RAG.DotNet;

public interface IQueryRewriter
{
    Task<string> RewriteAsync(string query, CancellationToken cancellationToken = default);
}