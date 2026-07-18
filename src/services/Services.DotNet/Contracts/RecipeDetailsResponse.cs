using Domain.DotNet;

namespace Services.DotNet.Contracts;

public class RecipeDetailsResponse
{
    public string? Error { get; set; }
    public string? Query { get; set; }
    public Recipe? Recipe { get; set; }
    public string? Message { get; set; }
}