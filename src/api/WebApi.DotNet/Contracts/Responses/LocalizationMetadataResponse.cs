namespace WebApi.DotNet.Contracts.Responses;

/// <summary>
/// Localization observability metadata returned by localized endpoints.
/// </summary>
public sealed class LocalizationMetadataResponse
{
    public string ResolvedLanguage { get; set; } = string.Empty;

    public string? FallbackLanguageUsed { get; set; }

    public IReadOnlyList<string> AvailableLanguages { get; set; } = Array.Empty<string>();
}
