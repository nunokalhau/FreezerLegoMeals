namespace Embedding.DotNet;

public sealed record EmbeddingResponse(string Model, int Dimensions, IReadOnlyList<float> Embedding);