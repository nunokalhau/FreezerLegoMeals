using Domain.DotNet;
using Microsoft.Extensions.Options;
using Repository.DotNet;
using Services.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;

namespace WebApi.DotNet.Services;

public interface IApiRecipeLocalizationService
{
    Task<GetLocalizedRecipeByIdResponse?> GetLocalizedRecipeByIdAsync(
        int recipeId,
        LocalizedRecipeQueryRequest request,
        HttpRequest httpRequest,
        CancellationToken cancellationToken = default);
}

public sealed class ApiRecipeLocalizationService : IApiRecipeLocalizationService
{
    private readonly ILocalizedRecipeQueryService _localizedRecipeQueryService;
    private readonly ILanguageContextResolver _languageContextResolver;
    private readonly ILocalizationOptionsFactory _localizationOptionsFactory;
    private readonly ApiLocalizationOptions _options;

    public ApiRecipeLocalizationService(
        ILocalizedRecipeQueryService localizedRecipeQueryService,
        ILanguageContextResolver languageContextResolver,
        ILocalizationOptionsFactory localizationOptionsFactory,
        IOptions<ApiLocalizationOptions> options)
    {
        _localizedRecipeQueryService = localizedRecipeQueryService ?? throw new ArgumentNullException(nameof(localizedRecipeQueryService));
        _languageContextResolver = languageContextResolver ?? throw new ArgumentNullException(nameof(languageContextResolver));
        _localizationOptionsFactory = localizationOptionsFactory ?? throw new ArgumentNullException(nameof(localizationOptionsFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<GetLocalizedRecipeByIdResponse?> GetLocalizedRecipeByIdAsync(
        int recipeId,
        LocalizedRecipeQueryRequest request,
        HttpRequest httpRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(httpRequest);

        var languageContext = _languageContextResolver.Resolve(
            explicitLanguage: request.Language,
            negotiatedLanguages: ParseNegotiatedLanguages(httpRequest),
            defaultLanguage: ResolveDefaultLanguage(),
            strictMode: request.StrictMode);

        var localizationOptions = _localizationOptionsFactory.Create(languageContext);

        var localizedRecipe = await _localizedRecipeQueryService.GetLocalizedRecipeByIdAsync(
            recipeId,
            localizationOptions,
            cancellationToken);

        if (localizedRecipe is null)
        {
            return null;
        }

        var availableLanguages = await ResolveAvailableLanguagesAsync(
            recipeId,
            localizedRecipe,
            localizationOptions,
            cancellationToken);

        return new GetLocalizedRecipeByIdResponse
        {
            Recipe = MapRecipe(localizedRecipe),
            Localization = new LocalizationMetadataResponse
            {
                ResolvedLanguage = localizedRecipe.Language,
                FallbackLanguageUsed = localizedRecipe.FallbackLanguageUsed,
                AvailableLanguages = availableLanguages
            }
        };
    }

    private LocalizedRecipeResponse MapRecipe(LocalizedRecipe localizedRecipe)
    {
        return new LocalizedRecipeResponse
        {
            CanonicalRecipeId = localizedRecipe.CanonicalRecipeId,
            Language = localizedRecipe.Language,
            Name = localizedRecipe.Name,
            Tags = localizedRecipe.Tags,
            Notes = localizedRecipe.Notes,
            Prepping = localizedRecipe.Prepping,
            TimeToPrepare = localizedRecipe.TimeToPrepare,
            Ingredients = localizedRecipe.Ingredients
                .Select(ingredient => new LocalizedRecipeIngredientResponse
                {
                    CanonicalIngredientId = ingredient.CanonicalIngredientId,
                    Language = ingredient.Language,
                    Name = ingredient.Name,
                    Amount = ingredient.Amount,
                    Unit = ingredient.Unit
                })
                .ToArray()
        };
    }

    private async Task<IReadOnlyList<string>> ResolveAvailableLanguagesAsync(
        int recipeId,
        LocalizedRecipe localizedRecipe,
        LocalizationOptions localizationOptions,
        CancellationToken cancellationToken)
    {
        var candidateLanguages = _options.SupportedLanguages
            .Concat([localizationOptions.PreferredLanguage, localizedRecipe.Language])
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var available = new List<string>();
        foreach (var language in candidateLanguages)
        {
            var probeOptions = LocalizationOptions.Create(language, strictMode: true);
            var probe = await _localizedRecipeQueryService.GetLocalizedRecipeByIdAsync(recipeId, probeOptions, cancellationToken);
            if (probe is not null)
            {
                available.Add(language);
            }
        }

        if (localizedRecipe.FallbackLanguageUsed is "canonical")
        {
            var defaultLanguage = ResolveDefaultLanguage();
            if (!available.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase))
            {
                available.Add(defaultLanguage);
            }
        }

        if (available.Count == 0)
        {
            available.Add(localizedRecipe.Language);
        }

        return available
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(language => language, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IEnumerable<string> ParseNegotiatedLanguages(HttpRequest request)
    {
        var headerValue = request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return Array.Empty<string>();
        }

        return headerValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim())
            .Where(language => !string.IsNullOrWhiteSpace(language) && !string.Equals(language, "*", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string ResolveDefaultLanguage()
    {
        return string.IsNullOrWhiteSpace(_options.DefaultLanguage)
            ? "en"
            : _options.DefaultLanguage.Trim();
    }
}
