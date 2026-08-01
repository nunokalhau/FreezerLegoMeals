namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Response DTO for localized recipe retrieval by canonical ID.
/// </summary>
public sealed class GetLocalizedRecipeByIdResponse
{
    public required LocalizedRecipeResponse Recipe { get; set; }

    public required LocalizationMetadataResponse Localization { get; set; }
}
