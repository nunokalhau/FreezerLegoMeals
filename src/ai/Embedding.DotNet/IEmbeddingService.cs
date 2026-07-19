namespace Embedding.DotNet;

public interface IEmbeddingService
{
    Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}