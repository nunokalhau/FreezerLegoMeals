namespace WebApi.DotNet.Contracts.Requests;

/// <summary>
/// Query contract for localized recipe retrieval.
/// </summary>
public sealed class LocalizedRecipeQueryRequest
{
    /// <summary>
    /// Explicit preferred language requested by client.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// When true, disables permissive fallback behavior.
    /// </summary>
    public bool StrictMode { get; set; }
}
