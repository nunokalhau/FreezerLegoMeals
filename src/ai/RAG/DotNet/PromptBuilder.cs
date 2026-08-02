using System.Text;
using System.Globalization;
using Domain.DotNet;

namespace RAG.DotNet;

public sealed class PromptBuilder : IPromptBuilder
{
    public string Build(
        string question,
        IReadOnlyList<RetrievalRecipe> recipes,
        string? intentType,
        LocalizationOptions localizationOptions,
        string? requestedLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(localizationOptions);

        var normalizedIntentType = NormalizeIntentType(intentType);
        var fallbackLanguage = localizationOptions.FallbackLanguages.FirstOrDefault();
        var fallbackChain = localizationOptions.FallbackLanguages.Count == 0
            ? "none"
            : string.Join(", ", localizationOptions.FallbackLanguages);

        var prompt = new StringBuilder();
        prompt.AppendLine("SYSTEM INSTRUCTIONS");
        prompt.AppendLine("- The retrieved recipe context below is the only source of truth.");
        prompt.AppendLine("- Use only facts present in the retrieved context.");
        prompt.AppendLine("- Do not invent recipes, ingredients, instructions, units, tags or nutrition facts.");
        prompt.AppendLine();

        prompt.AppendLine("LOCALIZATION RULES");
        prompt.AppendLine($"- Requested language: {ValueOrDefault(requestedLanguage)}");
        prompt.AppendLine($"- Resolved language: {localizationOptions.PreferredLanguage}");
        prompt.AppendLine($"- Strict mode: {localizationOptions.StrictMode}");
        prompt.AppendLine($"- Fallback language: {ValueOrDefault(fallbackLanguage)}");
        prompt.AppendLine($"- Fallback chain: {fallbackChain}");
        prompt.AppendLine("- Respond entirely in the resolved language.");
        prompt.AppendLine("- Never mix languages in the same answer.");
        prompt.AppendLine("- Never translate localized recipe names.");
        prompt.AppendLine("- Never translate localized ingredient names.");
        prompt.AppendLine("- Never translate localized tags, units or descriptions.");
        prompt.AppendLine("- Never invent translations.");
        prompt.AppendLine("- Treat retrieved localized fields as authoritative and preserve them exactly as retrieved.");
        prompt.AppendLine("- If localized information is unavailable for the resolved language, explicitly state that no localized result exists.");
        if (localizationOptions.StrictMode)
        {
            prompt.AppendLine("- Strict mode is enabled: never answer using another language.");
        }

        prompt.AppendLine();
        prompt.AppendLine("INTENT");
        prompt.AppendLine($"- Type: {normalizedIntentType}");
        prompt.AppendLine($"- Instructions: {GetIntentInstructions(normalizedIntentType)}");
        prompt.AppendLine();

        prompt.AppendLine("USER QUESTION");
        prompt.AppendLine(question.Trim());
        prompt.AppendLine();

        prompt.AppendLine("RETRIEVED RECIPES");
        prompt.AppendLine(FormatRecipes(recipes));
        prompt.AppendLine();

        prompt.AppendLine("OUTPUT EXPECTATIONS");
        prompt.AppendLine("- Base the answer only on retrieved recipes.");
        prompt.AppendLine("- Cite recipe titles exactly as retrieved when referencing recipes.");
        prompt.AppendLine("- If no relevant recipes are retrieved, state that clearly.");
        prompt.AppendLine($"- For {normalizedIntentType}: {GetOutputExpectation(normalizedIntentType)}");
        prompt.AppendLine("- Keep the answer concise and factual.");

        return prompt.ToString().TrimEnd();
    }

    private static string NormalizeIntentType(string? intentType)
    {
        if (string.IsNullOrWhiteSpace(intentType))
        {
            return "GeneralConversation";
        }

        return intentType.Trim();
    }

    private static string GetIntentInstructions(string? intentType)
    {
        return NormalizeIntentType(intentType) switch
        {
            "RecipeDiscovery" => "List every matching recipe found in context. Do not focus on only one recipe.",
            "RecipeDetails" => "Answer only about the requested recipe. Do not expand to other recipes unless explicitly requested.",
            "IngredientSearch" => "Explain which retrieved recipes contain the requested ingredient and cite recipe titles.",
            "MealPlanning" => "Generate a meal plan using only the retrieved recipes.",
            "GeneralConversation" => "Answer normally while still grounded in the provided repository context.",
            _ => "Answer normally while still grounded in the provided repository context."
        };
    }

    private static string GetOutputExpectation(string intentType)
    {
        return NormalizeIntentType(intentType) switch
        {
            "RecipeDiscovery" => "List all matching recipes from the retrieved set.",
            "RecipeDetails" => "Answer only about the requested recipe and avoid unrelated recipes.",
            "IngredientSearch" => "Explain which retrieved recipes contain the ingredient and identify each recipe.",
            "MealPlanning" => "Generate a meal plan using only retrieved recipes.",
            "GeneralConversation" => "Answer normally while remaining grounded in retrieved context.",
            _ => "Answer normally while remaining grounded in retrieved context."
        };
    }

    private static string FormatRecipes(IReadOnlyList<RetrievalRecipe> recipes)
    {
        if (recipes.Count == 0)
            return "No relevant recipes were retrieved.";

        return string.Join("\n\n", recipes.Select(FormatRecipe));
    }

    private static string FormatRecipe(RetrievalRecipe recipe) => string.Join("\n", new[]
    {
        $"Recipe ID: {recipe.RecipeId}",
        $"Title: {recipe.Title}",
        $"Description: {ValueOrDefault(recipe.Description)}",
        $"Tags: {ValueOrDefault(recipe.Tags)}",
        $"Ingredients: {(recipe.Ingredients.Count > 0 ? string.Join(", ", recipe.Ingredients) : "Not specified")}",
        $"Preparation steps: {ValueOrDefault(recipe.PreparationSteps)}",
        $"Cooking time: {ValueOrDefault(recipe.CookingTime)}",
        $"Similarity score: {recipe.SimilarityScore.ToString("F6", CultureInfo.InvariantCulture)}"
    });

    private static string ValueOrDefault(string? value) => string.IsNullOrWhiteSpace(value) ? "not specified" : value;
}