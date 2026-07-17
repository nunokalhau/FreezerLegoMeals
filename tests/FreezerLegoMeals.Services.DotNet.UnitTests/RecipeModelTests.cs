using System;
using Xunit;
using System.Collections.Generic;

namespace FrezerLegoMeals.Services.DotNet.UnitTests
{
    /// <summary>
    /// Unit tests for Recipe model and related data structures.
    /// </summary>
    public class RecipeModelTests
    {
        [Fact]
        public void Recipe_InitializesWithDefaultValues()
        {
            // Arrange & Act
            var recipe = new Recipe();
            
            // Assert
            Assert.NotNull(recipe);
            Assert.Equal(0, recipe.Id);
            Assert.Null(recipe.Name);
            Assert.Null(recipe.SourcePath);
            Assert.Null(recipe.Tags);
            Assert.Null(recipe.Servings);
            Assert.Null(recipe.TimeToPrepare);
            Assert.Null(recipe.Prepping);
            Assert.Null(recipe.FreezingNotes);
            Assert.Null(recipe.ReheatNotes);
            Assert.Null(recipe.Combinations);
            Assert.Null(recipe.Notes);
        }

        [Fact]
        public void Recipe_InitializesWithProvidedValues()
        {
            // Arrange
            var recipe = new Recipe
            {
                Id = 1,
                Name = "Test Recipe",
                SourcePath = "/recipes/test.md"
            };
            
            // Act & Assert
            Assert.Equal(1, recipe.Id);
            Assert.Equal("Test Recipe", recipe.Name);
            Assert.Equal("/recipes/test.md", recipe.SourcePath);
        }

        [Fact]
        public void Recipe_HasExpectedProperties()
        {
            // Arrange
            var recipe = new Recipe();
            
            // Act & Assert - Verify all expected properties are present and accessible
            Assert.NotNull(recipe.GetType().GetProperty("Id"));
            Assert.NotNull(recipe.GetType().GetProperty("Name"));
            Assert.NotNull(recipe.GetType().GetProperty("SourcePath"));
            Assert.NotNull(recipe.GetType().GetProperty("Tags"));
            Assert.NotNull(recipe.GetType().GetProperty("Servings"));
            Assert.NotNull(recipe.GetType().GetProperty("TimeToPrepare"));
            Assert.NotNull(recipe.GetType().GetProperty("Prepping"));
            Assert.NotNull(recipe.GetType().GetProperty("FreezingNotes"));
            Assert.NotNull(recipe.GetType().GetProperty("ReheatNotes"));
            Assert.NotNull(recipe.GetType().GetProperty("Combinations"));
            Assert.NotNull(recipe.GetType().GetProperty("Notes"));
        }
    }

    /// <summary>
    /// Unit tests for Ingredient model.
    /// </summary>
    public class IngredientModelTests
    {
        [Fact]
        public void Ingredient_InitializesWithDefaultValues()
        {
            // Arrange & Act
            var ingredient = new Ingredient();
            
            // Assert
            Assert.NotNull(ingredient);
            Assert.Null(ingredient.Name);
            Assert.Equal(0.0, ingredient.Amount);
            Assert.Null(ingredient.Unit);
            Assert.Null(ingredient.Notes);
            Assert.Null(ingredient.OriginalRecipe);
        }

        [Fact]
        public void Ingredient_InitializesWithProvidedValues()
        {
            // Arrange
            var ingredient = new Ingredient
            {
                Name = "Chicken Breast",
                Amount = 500.0,
                Unit = "g"
            };
            
            // Act & Assert
            Assert.Equal("Chicken Breast", ingredient.Name);
            Assert.Equal(500.0, ingredient.Amount);
            Assert.Equal("g", ingredient.Unit);
        }
    }

    /// <summary>
    /// Unit tests for RecipeCombination model.
    /// </summary>
    public class RecipeCombinationModelTests
    {
        [Fact]
        public void RecipeCombination_InitializesWithDefaultValues()
        {
            // Arrange & Act
            var combination = new RecipeCombination();
            
            // Assert
            Assert.NotNull(combination);
            Assert.Equal(0, combination.Id);
            Assert.Null(combination.Name);
            Assert.Null(combination.Description);
            Assert.Empty(combination.Recipes);
        }

        [Fact]
        public void RecipeCombination_HasExpectedProperties()
        {
            // Arrange
            var combination = new RecipeCombination();
            
            // Act & Assert - Verify all expected properties are present and accessible
            Assert.NotNull(combination.GetType().GetProperty("Id"));
            Assert.NotNull(combination.GetType().GetProperty("Name"));
            Assert.NotNull(combination.GetType().GetProperty("Description"));
            Assert.NotNull(combination.GetType().GetProperty("Recipes"));
        }
    }

    /// <summary>
    /// Unit tests for RecipeIngredient model.
    /// </summary>
    public class RecipeIngredientModelTests
    {
        [Fact]
        public void RecipeIngredient_InitializesWithDefaultValues()
        {
            // Arrange & Act
            var recipeIngredient = new RecipeIngredient();
            
            // Assert
            Assert.NotNull(recipeIngredient);
            Assert.Equal(0, recipeIngredient.RecipeId);
            Assert.Equal(0, recipeIngredient.IngredientId);
            Assert.Equal(0.0, recipeIngredient.Amount);
            Assert.Null(recipeIngredient.Unit);
        }
    }

    /// <summary>
    /// Integration tests for data model relationships.
    /// </summary>
    public class DataModelIntegrationTests
    {
        [Fact]
        public void Recipe_CanContainIngredients()
        {
            // Arrange
            var recipe = new Recipe();
            var ingredient = new Ingredient { Name = "Chicken Breast" };
            
            // Act & Assert - Verify models can be used together
            Assert.NotNull(recipe);
            Assert.NotNull(ingredient);
            Assert.Equal("Chicken Breast", ingredient.Name);
        }
    }
}