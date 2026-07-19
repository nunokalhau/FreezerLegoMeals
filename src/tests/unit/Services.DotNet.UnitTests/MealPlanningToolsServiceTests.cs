using System.Text.Json;
using Xunit;

namespace Services.DotNet.UnitTests;

public class MealPlanningToolsServiceTests
{
    [Fact]
    public async Task SearchRecipesTool_ReturnsRecipeDiscoveryCardsThroughPythonExecutor()
    {
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync("search_recipes", new Dictionary<string, object?>
        {
            ["ingredients"] = new[] { "chicken" },
            ["freezer_friendly"] = true,
            ["limit"] = 1
        });

        Assert.True(result.Success, result.Error);
        using var document = ToJson(result.Output!);
        var recipe = document.RootElement.GetProperty("recipes")[0];
        Assert.Equal("salsa_verde_chicken", recipe.GetProperty("id").GetString());
        Assert.Equal("Salsa Verde Chicken", recipe.GetProperty("name").GetString());
        Assert.Equal(0, recipe.GetProperty("cookTime").GetInt32());
        Assert.True(recipe.GetProperty("freezer_friendly").GetBoolean());
    }

    [Fact]
    public async Task BusinessTools_ReturnUsefulOutputsThroughPythonExecutor()
    {
        var executor = CreateExecutor();

        var weeklyPlan = await executor.ExecuteAsync("plan_weekly_meals", new Dictionary<string, object?> { ["number_of_days"] = 2, ["meals_per_day"] = 1 });
        var recipeDetails = await executor.ExecuteAsync("get_recipe", new Dictionary<string, object?> { ["id"] = "salsa_verde_chicken" });
        var replacement = await executor.ExecuteAsync("replace_meal", new Dictionary<string, object?> { ["current_recipe"] = "Turkey Chili", ["meal_type"] = "dinner" });
        var shoppingList = await executor.ExecuteAsync("optimize_shopping_list", new Dictionary<string, object?> { ["items"] = new[] { new { name = "Fresh Onion", amount = 1, unit = "unit" }, new { name = "onion", amount = 2, unit = "unit" } } });
        var batchPlan = await executor.ExecuteAsync("batch_cooking_plan", new Dictionary<string, object?> { ["recipes"] = new[] { "Salsa Verde Chicken" } });
        var converted = await executor.ExecuteAsync("convert_servings", new Dictionary<string, object?> { ["recipe"] = "salsa_verde_chicken", ["current_servings"] = 1, ["target_servings"] = 2 });
        var substitutions = await executor.ExecuteAsync("ingredient_substitutions", new Dictionary<string, object?> { ["ingredients"] = new[] { "chicken", "unknown" } });

        Assert.True(weeklyPlan.Success, weeklyPlan.Error);
        Assert.True(recipeDetails.Success, recipeDetails.Error);
        Assert.True(replacement.Success, replacement.Error);
        Assert.True(shoppingList.Success, shoppingList.Error);
        Assert.True(batchPlan.Success, batchPlan.Error);
        Assert.True(converted.Success, converted.Error);
        Assert.True(substitutions.Success, substitutions.Error);

        Assert.Equal(2, ToJson(weeklyPlan.Output!).RootElement.GetProperty("days").GetArrayLength());
        Assert.Equal(2, ToJson(recipeDetails.Output!).RootElement.GetProperty("numeric_id").GetInt32());
        Assert.Equal("dinner", ToJson(replacement.Output!).RootElement.GetProperty("meal_type").GetString());
        Assert.Equal("Produce", ToJson(shoppingList.Output!).RootElement.GetProperty("sections")[0].GetProperty("section").GetString());
        Assert.Equal(1, ToJson(batchPlan.Output!).RootElement.GetProperty("schedule")[0].GetProperty("step").GetInt32());
        Assert.Equal(2, ToJson(converted.Output!).RootElement.GetProperty("scale_factor").GetDouble());
        Assert.Equal("turkey", ToJson(substitutions.Output!).RootElement.GetProperty("suggestions")[0].GetProperty("substitutions")[0].GetProperty("ingredient").GetString());
    }

    private static JsonDocument ToJson(object value) => JsonDocument.Parse(JsonSerializer.Serialize(value));

    private static PythonToolExecutor CreateExecutor()
    {
        var repoRoot = FindRepoRoot();
        var toolsRoot = Path.Combine(repoRoot, "src", "tools");
        return new PythonToolExecutor(new ToolRegistry(Path.Combine(toolsRoot, "tool_registry.json")), toolsRoot);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "tools", "tool_registry.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
