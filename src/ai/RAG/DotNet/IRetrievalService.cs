namespace RAG.DotNet;

public interface IRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(string question, CancellationToken cancellationToken = default);
}