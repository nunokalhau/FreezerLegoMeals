namespace WebApi.DotNet.Contracts.Responses;

public sealed class SemanticSearchResponse
{
    public string RecipeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Score { get; set; }
    public string MatchedText { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}