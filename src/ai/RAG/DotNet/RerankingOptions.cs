namespace RAG.DotNet;

public sealed class RerankingOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(12);
}