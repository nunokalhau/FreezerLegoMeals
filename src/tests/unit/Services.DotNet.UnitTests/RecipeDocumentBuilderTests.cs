using Domain.DotNet;
using RAG.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class RecipeDocumentBuilderTests
{
    [Fact]
    public void Build_ProducesCanonicalDocumentWithRequiredSections()
    {
        var builder = new RecipeDocumentBuilder();
        var recipe = new Recipe
        {
            Name = "Spicy Chicken",
            Notes = "Great for batch cooking",
            Tags = "protein, spicy",
            Prepping = "Slice chicken and season",
            TimeToPrepare = 45,
            Servings = 4,
            FreezingNotes = "Freeze up to 3 months",
            ReheatNotes = "Microwave 2 min",
            RecipeIngredients =
            [
                new RecipeIngredient { Amount = 2, Unit = "tbsp", Ingredient = new Ingredient { Name = "Chili powder" } },
                new RecipeIngredient { Amount = 1.5, Unit = "lb", Ingredient = new Ingredient { Name = "Chicken" } }
            ]
        };

        var document = builder.Build(recipe);

        Assert.Contains("Title: Spicy Chicken", document);
        Assert.Contains("Description: Great for batch cooking", document);
        Assert.Contains("Tags: protein, spicy", document);
        Assert.Contains("Ingredients: 1.5 lb Chicken, 2 tbsp Chili powder", document);
        Assert.Contains("Preparation steps: Slice chicken and season", document);
        Assert.Contains("Cooking time: 45", document);
        Assert.Contains("Preparation time: 45", document);
        Assert.Contains("Servings: 4", document);
        Assert.Contains("Freezing instructions: Freeze up to 3 months", document);
        Assert.Contains("Reheating instructions: Microwave 2 min", document);
        Assert.Contains("Notes: Great for batch cooking", document);
    }

    [Fact]
    public void Build_IsDeterministic_ForEquivalentRecipesWithDifferentInputOrder()
    {
        var builder = new RecipeDocumentBuilder();

        var recipeA = new Recipe
        {
            Name = "Tofu Chorizo",
            Notes = "Great in tacos",
            Tags = "vegan, protein, spicy",
            Prepping = "Crumble tofu",
            TimeToPrepare = 30,
            Servings = 3,
            RecipeIngredients =
            [
                new RecipeIngredient { Amount = 1, Unit = "block", Ingredient = new Ingredient { Name = "Tofu" } },
                new RecipeIngredient { Amount = 1, Unit = "tbsp", Ingredient = new Ingredient { Name = "Paprika" } }
            ]
        };

        var recipeB = new Recipe
        {
            Name = "Tofu Chorizo",
            Notes = "Great in tacos",
            Tags = "spicy,protein,vegan",
            Prepping = "Crumble tofu",
            TimeToPrepare = 30,
            Servings = 3,
            RecipeIngredients =
            [
                new RecipeIngredient { Amount = 1, Unit = "tbsp", Ingredient = new Ingredient { Name = "Paprika" } },
                new RecipeIngredient { Amount = 1, Unit = "block", Ingredient = new Ingredient { Name = "Tofu" } }
            ]
        };

        var documentA = builder.Build(recipeA);
        var documentB = builder.Build(recipeB);

        Assert.Equal(documentA, documentB);
    }

    [Fact]
    public void Build_OmitsOptionalSectionsWhenUnavailable()
    {
        var builder = new RecipeDocumentBuilder();
        var recipe = new Recipe
        {
            Name = "Plain Rice",
            Tags = "starch",
            Prepping = "Rinse",
            TimeToPrepare = 20,
            Servings = 2,
            RecipeIngredients =
            [
                new RecipeIngredient { Amount = 1, Unit = "cup", Ingredient = new Ingredient { Name = "Rice" } }
            ]
        };

        var document = builder.Build(recipe);

        Assert.DoesNotContain("Freezing instructions:", document);
        Assert.DoesNotContain("Reheating instructions:", document);
        Assert.DoesNotContain("Notes:", document);
    }
}
