using System.Globalization;
using System.Text;
using Domain.DotNet;

namespace RAG.DotNet;

public sealed class RecipeDocumentBuilder : IRecipeDocumentBuilder
{
    public const string DefaultProjectionSchemaVersion = "recipe-semantic-projection-v1";

    public string Build(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return BuildProjection(new RecipeProjectionInput(recipe, "search-normalization-v1")).Document;
    }

    public RecipeProjection BuildProjection(RecipeProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Recipe);

        var recipe = input.Recipe;
        var languageCoverage = (input.LanguageCoverage ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Normalize(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var authoredSourceTexts = (input.AuthoredSourceTexts ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Normalize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var projectionSchemaVersion = string.IsNullOrWhiteSpace(input.ProjectionSchemaVersion)
            ? DefaultProjectionSchemaVersion
            : Normalize(input.ProjectionSchemaVersion);

        var ingredientValues = recipe.RecipeIngredients
            .OrderBy(ingredient => ingredient.IngredientId)
            .ThenBy(ingredient => Normalize(ingredient.Ingredient?.Name ?? string.Empty), StringComparer.Ordinal)
            .Select(FormatIngredient)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var tags = SplitAndNormalizeTags(recipe.Tags);
        var timeValue = recipe.TimeToPrepare?.ToString(CultureInfo.InvariantCulture) ?? "<none>";
        var servingsValue = recipe.Servings?.ToString(CultureInfo.InvariantCulture) ?? "<none>";
        var description = Normalize(recipe.Notes);
        var notes = Normalize(recipe.Notes);

        // Keep section order fixed so semantically identical recipes map to identical documents.
        var lines = new List<string>
        {
            $"Projection schema version: {projectionSchemaVersion}",
            $"Normalization version: {Normalize(input.NormalizationVersion)}",
            $"Language coverage: {(languageCoverage.Length == 0 ? "<none>" : string.Join(", ", languageCoverage))}",
            $"Title: {Normalize(recipe.Name)}",
            $"Description: {RenderOptional(description)}",
            $"Tags: {(tags.Length == 0 ? "<none>" : string.Join(", ", tags))}",
            $"Ingredients: {(ingredientValues.Length == 0 ? "<none>" : string.Join(", ", ingredientValues))}",
            $"Preparation steps: {Normalize(recipe.Prepping)}",
            $"Cooking time: {timeValue}",
            $"Preparation time: {timeValue}",
            $"Servings: {servingsValue}"
        };

        lines.Add($"Freezing instructions: {RenderOptional(recipe.FreezingNotes)}");
        lines.Add($"Reheating instructions: {RenderOptional(recipe.ReheatNotes)}");
        lines.Add($"Notes: {RenderOptional(notes)}");
        lines.Add($"Authored source text: {(authoredSourceTexts.Length == 0 ? "<none>" : string.Join(" | ", authoredSourceTexts))}");

        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                builder.AppendLine();

            builder.Append(lines[i]);
        }

        return new RecipeProjection(
            builder.ToString(),
            projectionSchemaVersion,
            Normalize(input.NormalizationVersion),
            languageCoverage,
            authoredSourceTexts);
    }

    private static string[] SplitAndNormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return [];

        return tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FormatIngredient(RecipeIngredient ingredient)
    {
        if (ingredient is null)
            return string.Empty;

        var parts = new List<string>();
        if (ingredient.Amount is double amount)
            parts.Add(amount.ToString("0.###", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(ingredient.Unit))
            parts.Add(Normalize(ingredient.Unit));
        if (!string.IsNullOrWhiteSpace(ingredient.Ingredient?.Name))
            parts.Add(Normalize(ingredient.Ingredient.Name));

        return string.Join(" ", parts);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(" ", value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string RenderOptional(string? value)
    {
        var normalized = Normalize(value);
        return string.IsNullOrWhiteSpace(normalized) ? "<none>" : normalized;
    }
}
