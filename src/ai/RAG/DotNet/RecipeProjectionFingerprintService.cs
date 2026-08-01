using System.Security.Cryptography;
using System.Text;

namespace RAG.DotNet;

public sealed class RecipeProjectionFingerprintService : IRecipeProjectionFingerprintService
{
    public string Compute(RecipeProjectionFingerprintInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var builder = new StringBuilder();
        builder.AppendLine($"recipeId={input.RecipeId}");
        builder.AppendLine($"projectionSchemaVersion={input.ProjectionSchemaVersion}");
        builder.AppendLine($"normalizationVersion={input.NormalizationVersion}");
        builder.AppendLine("languageCoverage=" + Join(input.LanguageCoverage));
        builder.AppendLine("translationContentHashes=" + Join(input.TranslationContentHashes));
        builder.AppendLine("ingredientTranslationContentHashes=" + Join(input.IngredientTranslationContentHashes));
        builder.AppendLine("recipeIngredientLocalizationContentHashes=" + Join(input.RecipeIngredientLocalizationContentHashes));
        builder.AppendLine("canonicalDependencyHashes=" + Join(input.CanonicalDependencyHashes));
        builder.AppendLine("authoredSourceContributions=" + Join(input.AuthoredSourceContributions));
        builder.Append("projectionDocument=");
        builder.Append(input.ProjectionDocument);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Join(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return string.Empty;

        return string.Join("|", values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .OrderBy(value => value, StringComparer.Ordinal));
    }
}
