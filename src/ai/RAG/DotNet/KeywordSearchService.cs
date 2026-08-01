using Domain.DotNet;
using Repository.DotNet;
using SemanticSearch.DotNet;

namespace RAG.DotNet;

public sealed class KeywordSearchService : IKeywordSearchService
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly ISearchQueryNormalizer _searchQueryNormalizer;

    public KeywordSearchService(IRecipeRepository recipeRepository, ISearchQueryNormalizer? searchQueryNormalizer = null)
    {
        _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
        _searchQueryNormalizer = searchQueryNormalizer ?? new DefaultSearchQueryNormalizer();
    }

    public async Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedQuery = _searchQueryNormalizer.Normalize(query);
        var terms = normalizedQuery.ExpandedTokens
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (terms.Count == 0)
            return [];

        var recipes = await _recipeRepository.GetRecipesAsync();
        var scored = new List<KeywordSearchResult>();

        foreach (var recipe in recipes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var searchableText = BuildSearchableText(recipe);
            if (string.IsNullOrWhiteSpace(searchableText))
                continue;

            var score = ScoreRecipe(searchableText, normalizedQuery.NormalizedQuery, terms);
            if (score <= 0)
                continue;

            scored.Add(new KeywordSearchResult(recipe.Id.ToString(), score));
        }

        return scored
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.RecipeId, StringComparer.Ordinal)
            .Take(topK)
            .ToList();
    }

    private static string BuildSearchableText(Recipe recipe)
    {
        var values = new List<string>
        {
            recipe.Name ?? string.Empty,
            recipe.Tags ?? string.Empty,
            recipe.Notes ?? string.Empty,
            recipe.Prepping ?? string.Empty,
            string.Join(' ', recipe.RecipeIngredients
                .Select(ingredient => ingredient.Ingredient?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)))
        };

        return string.Join(' ', values).ToLowerInvariant();
    }

    private static double ScoreRecipe(string searchableText, string fullQuery, IReadOnlyList<string> terms)
    {
        var score = 0d;
        foreach (var term in terms)
        {
            if (searchableText.Contains(term, StringComparison.Ordinal))
            {
                score += 1d;
            }
        }

        if (score == 0)
            return 0;

        if (searchableText.Contains(fullQuery.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            score += 2d;
        }

        return score;
    }
}