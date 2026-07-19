namespace WebApi.DotNet.Contracts.Responses;

public sealed class EmbeddingResponse
{
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public IReadOnlyList<float> Embedding { get; set; } = Array.Empty<float>();
}