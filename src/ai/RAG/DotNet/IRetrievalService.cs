namespace RAG.DotNet;

using Domain.DotNet;

public interface IRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(string question, CancellationToken cancellationToken = default);

    Task<RetrievalResult> RetrieveAsync(string question, LocalizationOptions localizationOptions, CancellationToken cancellationToken = default);

    Task<RetrievalResult> RetrieveAsync(RetrievalRequestContext requestContext, CancellationToken cancellationToken = default);
}