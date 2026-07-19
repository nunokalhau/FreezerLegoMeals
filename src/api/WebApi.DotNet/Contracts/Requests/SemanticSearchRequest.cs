namespace WebApi.DotNet.Contracts.Requests;

public sealed class SemanticSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}