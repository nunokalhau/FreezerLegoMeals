using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FreezerLegoMeals.Repository.DotNet.Entities;

namespace FreezerLegoMeals.Repository.DotNet.Testing;

/// <summary>
/// Test fixture for in-memory database context setup for repository testing.
/// Provides a proper EF Core InMemory database context with test data.
/// </summary>
public class InMemoryDbContextFixture : IDisposable
{
    public FreezerLegoMealsContext Context { get; private set; }
    
    public InMemoryDbContextFixture()
    {
        var options = new DbContextOptionsBuilder<FreezerLegoMealsContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .Options;
            
        Context = new FreezerLegoMealsContext(options);
        
        // Seed the database with test data
        SeedDatabase();
    }
    
    private void SeedDatabase()
    {
        // Clear any existing data
        Context.Recipes.RemoveRange(Context.Recipes);
        Context.Ingredients.RemoveRange(Context.Ingredients);
        Context.RecipeCombinations.RemoveRange(Context.RecipeCombinations);
        Context.RecipeIngredients.RemoveRange(Context.RecipeIngredients);
        Context.RecipeCombinationItems.RemoveRange(Context.RecipeCombinationItems);
        
        // Add test ingredients
        var ingredients = new List<IngredientEntity>
        {
            new IngredientEntity { Id = 1, Name = "Chicken" },
            new IngredientEntity { Id = 2, Name = "Onion" },
            new IngredientEntity { Id = 3, Name = "Garlic" },
            new IngredientEntity { Id = 4, Name = "Rice" }
        };
        
        Context.Ingredients.AddRange(ingredients);
        
        // Add test recipes
        var recipes = new List<RecipeEntity>
        {
            new RecipeEntity 
            { 
                Id = 1, 
                Name = "Chicken Soup", 
                SourcePath = "/recipes/chicken-soup.md",
                Tags = "soup, chicken, healthy",
                Servings = 4,
                TimeToPrepare = 30,
                Prepping = "Chop vegetables",
                FreezingNotes = "Freezes well",
                ReheatNotes = "Reheat on stove",
                Combinations = "1,2"
            },
            new RecipeEntity 
            { 
                Id = 2, 
                Name = "Chicken and Rice", 
                SourcePath = "/recipes/chicken-rice.md",
                Tags = "rice, chicken, one-pot",
                Servings = 2,
                TimeToPrepare = 45,
                Prepping = "Marinate chicken for 30 mins",
                FreezingNotes = "Freezes well",
                ReheatNotes = "Microwave until heated through",
                Combinations = "1"
            }
        };
        
        Context.Recipes.AddRange(recipes);
        
        // Add recipe ingredients
        var recipeIngredients = new List<RecipeIngredientEntity>
        {
            new RecipeIngredientEntity { RecipeId = 1, IngredientId = 1, Amount = 500, Unit = "g" },
            new RecipeIngredientEntity { RecipeId = 1, IngredientId = 2, Amount = 1, Unit = "large" },
            new RecipeIngredientEntity { RecipeId = 1, IngredientId = 3, Amount = 2, Unit = "cloves" },
            new RecipeIngredientEntity { RecipeId = 2, IngredientId = 1, Amount = 200, Unit = "g" },
            new RecipeIngredientEntity { RecipeId = 2, IngredientId = 4, Amount = 1, Unit = "cup" }
        };
        
        Context.RecipeIngredients.AddRange(recipeIngredients);
        
        // Add recipe combinations
        var combinations = new List<RecipeCombinationEntity>
        {
            new RecipeCombinationEntity 
            { 
                Id = 1, 
                Name = "Weekend Meal Combo", 
                Description = "A combination of chicken and rice recipes" 
            }
        };
        
        Context.RecipeCombinations.AddRange(combinations);
        
        // Add recipe combination items
        var combinationItems = new List<RecipeCombinationItemEntity>
        {
            new RecipeCombinationItemEntity { Id = 1, CombinationId = 1, RecipeId = 1, Position = 1 },
            new RecipeCombinationItemEntity { Id = 2, CombinationId = 1, RecipeId = 2, Position = 2 }
        };
        
        Context.RecipeCombinationItems.AddRange(combinationItems);
        
        // Save changes to initialize the test data
        Context.SaveChanges();
    }
    
    public void Dispose()
    {
        Context?.Dispose();
    }
}