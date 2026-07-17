using System;
using System.Threading.Tasks;
using Xunit;
using FreezerLegoMeals.Repository.DotNet;

namespace FreezerLegoMeals.Repository.DotNet.UnitTests
{
    /// <summary>
    /// Unit tests for the .NET repository layer.
    /// </summary>
    public class RepositoryLayerTests : IDisposable
    {
        private readonly InMemoryDbContextFixture _fixture;
        private readonly RecipeRepository _repository;

        public RepositoryLayerTests()
        {
            _fixture = new InMemoryDbContextFixture();
            _repository = new RecipeRepository(_fixture.Context);
        }

        public void Dispose()
        {
            _fixture?.Dispose();
        }

        [Fact]
        public async Task GetRecipesAsync_Returns_All_Recipes_With_Ingredients()
        {
            // Act
            var result = await _repository.GetRecipesAsync();
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal(3, result.Count());
            
            // Verify recipe properties
            var firstRecipe = result.First();
            Assert.Equal("Chicken Fried Rice", firstRecipe.Name);
            Assert.Equal("chicken,rice,fast", firstRecipe.Tags);
            Assert.Equal(4, firstRecipe.Servings);
            Assert.NotNull(firstRecipe.RecipeIngredients);
            Assert.NotEmpty(firstRecipe.RecipeIngredients);
            
            // Verify ingredient relationships
            var ingredients = firstRecipe.RecipeIngredients.ToList();
            Assert.Contains(ingredients, ri => ri.Ingredient.Name == "Chicken");
            Assert.Contains(ingredients, ri => ri.Ingredient.Name == "Rice");
        }

        [Fact]
        public async Task GetRecipeByIdAsync_Returns_Recipe_With_Correct_Data()
        {
            // Act
            var result = await _repository.GetRecipeByIdAsync(1);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Chicken Fried Rice", result.Name);
            Assert.Equal("chicken,rice,fast", result.Tags);
            Assert.Equal(4, result.Servings);
            Assert.NotNull(result.RecipeIngredients);
            Assert.NotEmpty(result.RecipeIngredients);
            
            // Verify ingredient relationships
            var ingredients = result.RecipeIngredients.ToList();
            Assert.Contains(ingredients, ri => ri.Ingredient.Name == "Chicken");
            Assert.Contains(ingredients, ri => ri.Ingredient.Name == "Rice");
        }

        [Fact]
        public async Task GetRecipeByIdAsync_Returns_Null_For_Non_Existent_Recipe()
        {
            // Act
            var result = await _repository.GetRecipeByIdAsync(999);
            
            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task FindRecipesWithIngredientsAsync_Finds_Recipes_Containing_Ingredients()
        {
            // Act
            var result = await _repository.FindRecipesWithIngredientsAsync(new[] { "Chicken" });
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Single(result); // Should find just the Chicken Fried Rice recipe
            
            var recipe = result.First();
            Assert.Equal(1, recipe.Id);
            Assert.Equal("Chicken Fried Rice", recipe.Name);
        }

        [Fact]
        public async Task FindRecipesWithIngredientsAsync_Returns_Empty_When_No_Matching_Ingredients()
        {
            // Act
            var result = await _repository.FindRecipesWithIngredientsAsync(new[] { "Pasta" });
            
            // Assert
            Assert.NotNull(result);
            Assert.Empty(result); // Should return empty collection
        }

        [Fact]
        public async Task FindRecipesWithIngredientsAsync_Returns_Multiple_Recipes()
        {
            // Act
            var result = await _repository.FindRecipesWithIngredientsAsync(new[] { "Beef" });
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal(2, result.Count()); // Should find both beef stir fry and broccoli beef
            
            var recipeNames = result.Select(r => r.Name).ToList();
            Assert.Contains("Beef Stir Fry", recipeNames);
            Assert.Contains("Broccoli Beef", recipeNames);
        }

        [Fact]
        public async Task GetCombinationsAsync_Returns_All_Combinations()
        {
            // Act
            var result = await _repository.GetCombinationsAsync();
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal(2, result.Count());
            
            var combinations = result.ToList();
            Assert.Contains(combinations, c => c.Name == "Main Meal");
            Assert.Contains(combinations, c => c.Name == "Side Dish");
        }

        [Fact]
        public async Task GetCombinationByIdAsync_Returns_Combination_With_Recipes()
        {
            // Act
            var result = await _repository.GetCombinationByIdAsync(1);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Main Meal", result.Name);
            Assert.NotNull(result.RecipeCombinationItems);
            Assert.NotEmpty(result.RecipeCombinationItems);
        }

        [Fact]
        public async Task GetCombinationByIdAsync_Returns_Null_For_Non_Existent_Combination()
        {
            // Act
            var result = await _repository.GetCombinationByIdAsync(999);
            
            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetIngredientsAsync_Returns_All_Ingredients()
        {
            // Act
            var result = await _repository.GetIngredientsAsync();
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal(5, result.Count());
            
            var ingredientNames = result.Select(i => i.Name).ToList();
            Assert.Contains("Chicken", ingredientNames);
            Assert.Contains("Rice", ingredientNames);
            Assert.Contains("Broccoli", ingredientNames);
            Assert.Contains("Beef", ingredientNames);
            Assert.Contains("Tomato", ingredientNames);
        }

        [Fact]
        public async Task GetIngredientByNameAsync_Returns_Ingredient_With_Correct_Data()
        {
            // Act
            var result = await _repository.GetIngredientByNameAsync("Chicken");
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Chicken", result.Name);
        }

        [Fact]
        public async Task GetIngredientByNameAsync_Returns_Null_For_Non_Existent_Ingredient()
        {
            // Act
            var result = await _repository.GetIngredientByNameAsync("Pasta");
            
            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetIngredientByNameAsync_Returns_Null_For_Empty_String()
        {
            // Act
            var result = await _repository.GetIngredientByNameAsync(string.Empty);
            
            // Assert
            Assert.Null(result);
        }
    }
}