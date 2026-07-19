using System.Globalization;

namespace RAG.DotNet;

public sealed class PromptBuilder : IPromptBuilder
{
    private readonly string _template;

    public PromptBuilder(string template)
    {
        _template = string.IsNullOrWhiteSpace(template) ? throw new ArgumentException("Prompt template is required", nameof(template)) : template;
    }

    public static PromptBuilder FromFile(string templatePath) => new(File.ReadAllText(templatePath));

    public string Build(string question, IReadOnlyList<RetrievalRecipe> recipes) =>
        _template
            .Replace("{recipes}", FormatRecipes(recipes), StringComparison.Ordinal)
            .Replace("{question}", question.Trim(), StringComparison.Ordinal);

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

    private static string ValueOrDefault(string value) => string.IsNullOrWhiteSpace(value) ? "Not specified" : value;
}