namespace Embedding.DotNet;

public sealed class EmbeddingOptions
{
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "nomic-embed-text";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}