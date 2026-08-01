using System.Text.RegularExpressions;
using RAG.DotNet;

namespace Services.DotNet;

public sealed class AnswerGroundingService : IAnswerGroundingService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "for", "from", "has", "have", "how",
        "i", "if", "in", "is", "it", "its", "me", "my", "of", "on", "or", "that", "the", "their", "them",
        "there", "these", "this", "to", "use", "was", "we", "what", "when", "where", "which", "with", "you", "your"
    };

    public Task<AnswerGroundingResult> ValidateAsync(
        string answer,
        IReadOnlyList<RetrievalRecipe> retrievedRecipes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return Task.FromResult(new AnswerGroundingResult(false, 1));
        }

        if (retrievedRecipes is null || retrievedRecipes.Count == 0)
        {
            return Task.FromResult(new AnswerGroundingResult(false, 1));
        }

        var contextTokens = BuildContextTokenSet(retrievedRecipes);
        var claims = ExtractClaims(answer);
        if (claims.Count == 0)
        {
            return Task.FromResult(new AnswerGroundingResult(true, 0));
        }

        var unsupportedClaims = 0;
        foreach (var claim in claims)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claimTokens = Tokenize(claim);
            if (claimTokens.Count == 0)
            {
                continue;
            }

            var matched = claimTokens.Count(token => contextTokens.Contains(token));
            var ratio = (double)matched / claimTokens.Count;
            var supported = matched >= 2 || ratio >= 0.75;
            if (!supported)
            {
                unsupportedClaims++;
            }
        }

        return Task.FromResult(new AnswerGroundingResult(unsupportedClaims == 0, unsupportedClaims));
    }

    private static HashSet<string> BuildContextTokenSet(IReadOnlyList<RetrievalRecipe> recipes)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipe in recipes)
        {
            AddTokens(tokens, recipe.RecipeId);
            AddTokens(tokens, recipe.Title);
            AddTokens(tokens, recipe.Description);
            AddTokens(tokens, recipe.Tags);
            AddTokens(tokens, recipe.PreparationSteps);
            AddTokens(tokens, recipe.CookingTime);
            foreach (var ingredient in recipe.Ingredients)
            {
                AddTokens(tokens, ingredient);
            }
        }

        return tokens;
    }

    private static IReadOnlyList<string> ExtractClaims(string answer)
    {
        return answer
            .Split(['.', '!', '?', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => segment.Length >= 4)
            .ToList();
    }

    private static void AddTokens(ISet<string> destination, string? content)
    {
        foreach (var token in Tokenize(content))
        {
            destination.Add(token);
        }
    }

    private static IReadOnlyList<string> Tokenize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return Regex.Matches(content, "[a-zA-Z0-9]{3,}")
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}