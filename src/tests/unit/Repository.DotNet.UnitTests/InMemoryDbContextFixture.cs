using Microsoft.EntityFrameworkCore;
using Repository.DotNet.Entities;

namespace Repository.DotNet.UnitTests;

/// <summary>
/// Test fixture for in-memory database context used in repository tests.
/// Provides a clean, isolated database for each test run.
/// </summary>
public class InMemoryDbContextFixture : IDisposable
{
    public FreezerLegoMealsContext Context { get; private set; }

    public InMemoryDbContextFixture()
    {
        var databaseName = $"TestDatabase_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<FreezerLegoMealsContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;
            
        Context = new FreezerLegoMealsContext(options);
            
        // Seed the database with test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Clear existing data
        Context.RecipeCombinations.RemoveRange(Context.RecipeCombinations);
        Context.RecipeIngredients.RemoveRange(Context.RecipeIngredients);
        Context.RecipeCombinationItems.RemoveRange(Context.RecipeCombinationItems);
        Context.Recipes.RemoveRange(Context.Recipes);
        Context.Ingredients.RemoveRange(Context.Ingredients);
            
        // Add test ingredients
        var ingredient1 = new IngredientEntity { Id = 1, Name = "Chicken" };
        var ingredient2 = new IngredientEntity { Id = 2, Name = "Rice" };
        var ingredient3 = new IngredientEntity { Id = 3, Name = "Broccoli" };
        var ingredient4 = new IngredientEntity { Id = 4, Name = "Beef" };
        var ingredient5 = new IngredientEntity { Id = 5, Name = "Tomato" };
            
        Context.Ingredients.AddRange(ingredient1, ingredient2, ingredient3, ingredient4, ingredient5);
            
        // Add test combinations
        var combination1 = new RecipeCombinationEntity 
        { 
            Id = 1, 
            Name = "Main Meal", 
            Description = "Primary meal combination" 
        };
            
        var combination2 = new RecipeCombinationEntity 
        { 
            Id = 2, 
            Name = "Side Dish", 
            Description = "Side dish combination" 
        };
            
        Context.RecipeCombinations.AddRange(combination1, combination2);
            
        // Add test recipes
        var recipe1 = new RecipeEntity
        {
            Id = 1,
            Name = "Chicken Fried Rice",
            SourcePath = "/recipes/chicken_fried_rice.md",
            Tags = "chicken,rice,fast",
            Servings = 4,
            TimeToPrepare = 30,
            Prepping = "Chop ingredients",
            FreezingNotes = "Freezes well for up to 3 months",
            ReheatNotes = "Reheat with steam",
            Combinations = "1",
            Notes = "Delicious chicken fried rice recipe"
        };
            
        var recipe2 = new RecipeEntity
        {
            Id = 2,
            Name = "Beef Stir Fry",
            SourcePath = "/recipes/beef_stir_fry.md",
            Tags = "beef,vegetables,quick",
            Servings = 3,
            TimeToPrepare = 25,
            Prepping = "Cut vegetables",
            FreezingNotes = "Freezes well for up to 2 months",
            ReheatNotes = "Reheat in microwave",
            Combinations = "1,2",
            Notes = "Quick beef stir fry recipe"
        };
            
        var recipe3 = new RecipeEntity
        {
            Id = 3,
            Name = "Broccoli Beef",
            SourcePath = "/recipes/broccoli_beef.md",
            Tags = "beef,vegetables,healthy",
            Servings = 2,
            TimeToPrepare = 35,
            Prepping = "Clean vegetables",
            FreezingNotes = "Freezes well for up to 4 months",
            ReheatNotes = "Reheat on stove",
            Combinations = "2",
            Notes = "Healthy broccoli beef recipe"
        };
            
        Context.Recipes.AddRange(recipe1, recipe2, recipe3);
            
        // Add recipe ingredient relationships
        var recipeIngredient1 = new RecipeIngredientEntity 
        { 
            RecipeId = 1, 
            IngredientId = 1,
            Amount = 1.0,
            Unit = "lb"
        };
            
        var recipeIngredient2 = new RecipeIngredientEntity 
        { 
            RecipeId = 1, 
            IngredientId = 2,
            Amount = 2.0,
            Unit = "cups"
        };
            
        var recipeIngredient3 = new RecipeIngredientEntity 
        { 
            RecipeId = 2, 
            IngredientId = 4,
            Amount = 1.5,
            Unit = "lb"
        };
            
        var recipeIngredient4 = new RecipeIngredientEntity 
        { 
            RecipeId = 2, 
            IngredientId = 3,
            Amount = 1.0,
            Unit = "head"
        };
            
        var recipeIngredient5 = new RecipeIngredientEntity 
        { 
            RecipeId = 3, 
            IngredientId = 4,
            Amount = 1.0,
            Unit = "lb"
        };
            
        var recipeIngredient6 = new RecipeIngredientEntity 
        { 
            RecipeId = 3, 
            IngredientId = 3,
            Amount = 2.0,
            Unit = "cups"
        };
            
        Context.RecipeIngredients.AddRange(recipeIngredient1, recipeIngredient2, 
                                            recipeIngredient3, recipeIngredient4, 
                                            recipeIngredient5, recipeIngredient6);
            
        // Add recipe combination items
        var combinationItem1 = new RecipeCombinationItemEntity
        {
            Id = 1,
            CombinationId = 1,
            RecipeId = 1,
            Position = 1
        };
            
        var combinationItem2 = new RecipeCombinationItemEntity
        {
            Id = 2,
            CombinationId = 1,
            RecipeId = 2,
            Position = 2
        };
            
        var combinationItem3 = new RecipeCombinationItemEntity
        {
            Id = 3,
            CombinationId = 2,
            RecipeId = 3,
            Position = 1
        };
            
        Context.RecipeCombinationItems.AddRange(combinationItem1, combinationItem2, combinationItem3);
            
        // Save changes to database
        Context.SaveChanges();
    }

    public void Dispose()
    {
        Context?.Dispose();
    }
}