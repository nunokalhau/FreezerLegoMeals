using System.Globalization;
using System.Text;
using Domain.DotNet;

namespace RAG.DotNet;

public sealed class RecipeDocumentBuilder : IRecipeDocumentBuilder
{
    public string Build(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var ingredientValues = recipe.RecipeIngredients
            .Select(FormatIngredient)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tags = SplitAndNormalizeTags(recipe.Tags);
        var timeValue = recipe.TimeToPrepare?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var servingsValue = recipe.Servings?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var description = Normalize(recipe.Notes);
        var notes = Normalize(recipe.Notes);

        // Keep section order fixed so semantically identical recipes map to identical documents.
        var lines = new List<string>
        {
            $"Title: {Normalize(recipe.Name)}",
            $"Description: {description}",
            $"Tags: {string.Join(", ", tags)}",
            $"Ingredients: {string.Join(", ", ingredientValues)}",
            $"Preparation steps: {Normalize(recipe.Prepping)}",
            $"Cooking time: {timeValue}",
            $"Preparation time: {timeValue}",
            $"Servings: {servingsValue}"
        };

        var freezing = Normalize(recipe.FreezingNotes);
        if (!string.IsNullOrWhiteSpace(freezing))
            lines.Add($"Freezing instructions: {freezing}");

        var reheating = Normalize(recipe.ReheatNotes);
        if (!string.IsNullOrWhiteSpace(reheating))
            lines.Add($"Reheating instructions: {reheating}");

        if (!string.IsNullOrWhiteSpace(notes))
            lines.Add($"Notes: {notes}");

        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                builder.AppendLine();

            builder.Append(lines[i]);
        }

        return builder.ToString();
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
}
