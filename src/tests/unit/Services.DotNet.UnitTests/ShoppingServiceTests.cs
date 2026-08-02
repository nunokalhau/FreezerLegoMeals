using Xunit;
using Moq;
using Domain.DotNet;
using Repository.DotNet;
using Services.DotNet.Contracts;

namespace Services.DotNet.UnitTests;

/// <summary>
/// Unit tests for the ShoppingService in the .NET service layer.
/// Tests business logic with mocked repository dependencies.
/// </summary>
public class ShoppingServiceTests
{
    private readonly Mock<IRecipeRepository> _mockRepository;
    private readonly ShoppingService _service;

    public ShoppingServiceTests()
    {
        _mockRepository = new Mock<IRecipeRepository>();
        var generator = new DeterministicShoppingListGenerator(_mockRepository.Object);
        var formatter = new DeterministicShoppingListFormatter();
        _service = new ShoppingService(_mockRepository.Object, generator, formatter);
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException when repository is null
    /// </summary>
    [Fact]
    public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var generator = new DeterministicShoppingListGenerator(_mockRepository.Object);
        var formatter = new DeterministicShoppingListFormatter();
        Assert.Throws<ArgumentNullException>(() => new ShoppingService(null, generator, formatter));
    }

    /// <summary>
    /// Tests GetRecipeIngredientsAsync with valid numeric identifier and existing recipe
    /// </summary>
    [Fact]
    public async Task GetRecipeIngredientsAsync_WithValidNumericIdentifierAndExistingRecipe_ShouldCallRepository()
    {
        // Arrange
        var recipeId = "123";
        var mockRecipe = new Recipe { Id = 123, Name = "Chicken Curry" };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(123))
                      .ReturnsAsync(mockRecipe);

        // Act
        var result = await _service.GetRecipeIngredientsAsync(recipeId);

        // Assert
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(123), Times.Once);
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GetRecipeIngredientsAsync with valid numeric identifier but non-existent recipe
    /// </summary>
    [Fact]
    public async Task GetRecipeIngredientsAsync_WithValidNumericIdentifierAndNonExistentRecipe_ShouldReturnEmpty()
    {
        // Arrange
        var recipeId = "456";
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(456))
                      .ReturnsAsync((Recipe)null);

        // Act
        var result = await _service.GetRecipeIngredientsAsync(recipeId);

        // Assert
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(456), Times.Once);
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GetRecipeIngredientsAsync with valid textual identifier and existing recipe
    /// </summary>
    [Fact]
    public async Task GetRecipeIngredientsAsync_WithValidTextIdentifierAndExistingRecipe_ShouldCallRepository()
    {
        // Arrange
        var recipeName = "Chicken Curry";
        var mockRecipes = new List<Recipe> { new Recipe { Id = 123, Name = "Chicken Curry" } };
        _mockRepository.Setup(r => r.FindRecipesWithIngredientsAsync(new[] { recipeName }))
                      .ReturnsAsync(mockRecipes);
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(123))
                      .ReturnsAsync((Recipe)null);

        // Act
        var result = await _service.GetRecipeIngredientsAsync(recipeName);

        // Assert
        _mockRepository.Verify(r => r.FindRecipesWithIngredientsAsync(new[] { recipeName }), Times.Once);
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(123), Times.Once);
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GetRecipeIngredientsAsync with valid textual identifier and no matching recipe
    /// </summary>
    [Fact]
    public async Task GetRecipeIngredientsAsync_WithValidTextIdentifierAndNoMatchingRecipe_ShouldReturnEmpty()
    {
        // Arrange
        var recipeName = "NonExistent Recipe";
        _mockRepository.Setup(r => r.FindRecipesWithIngredientsAsync(new[] { recipeName }))
                      .ReturnsAsync(new List<Recipe>());

        // Act
        var result = await _service.GetRecipeIngredientsAsync(recipeName);

        // Assert
        _mockRepository.Verify(r => r.FindRecipesWithIngredientsAsync(new[] { recipeName }), Times.Once);
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GetRecipeIngredientsAsync with null identifier
    /// </summary>
    [Fact]
    public void GetRecipeIngredientsAsync_WithNullIdentifier_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetRecipeIngredientsAsync(null));
    }

    /// <summary>
    /// Tests GetRecipeIngredientsAsync with empty identifier
    /// </summary>
    [Fact]
    public void GetRecipeIngredientsAsync_WithEmptyIdentifier_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetRecipeIngredientsAsync(""));
    }

    /// <summary>
    /// Tests GetMultipleRecipeIngredientsAsync with valid recipe identifiers
    /// </summary>
    [Fact]
    public async Task GetMultipleRecipeIngredientsAsync_WithValidIdentifiers_ShouldCallRepository()
    {
        // Arrange
        var identifiers = new List<string> { "123", "456" };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(It.IsAny<int>()))
                      .ReturnsAsync((Recipe)null);

        // Act
        var result = await _service.GetMultipleRecipeIngredientsAsync(identifiers);

        // Assert
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(It.IsAny<int>()), Times.Exactly(2));
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GetMultipleRecipeIngredientsAsync with null identifiers list
    /// </summary>
    [Fact]
    public void GetMultipleRecipeIngredientsAsync_WithNullIdentifiers_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetMultipleRecipeIngredientsAsync(null));
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync with valid meal plan recipe IDs
    /// </summary>
    [Fact]
    public async Task GenerateShoppingListAsync_WithValidMealPlan_ShouldReturnExpectedResult()
    {
        // Arrange
        var mealPlan = new MealPlan { RecipeIds = new List<int> { 123, 456 } };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(It.IsAny<int>()))
                      .ReturnsAsync(new Recipe
                      {
                          Id = 123,
                          Name = "Test Recipe",
                          SourcePath = "food/proteins/test.md",
                          RecipeIngredients =
                          [
                              new RecipeIngredient
                              {
                                  RecipeId = 123,
                                  IngredientId = 1,
                                  Amount = 2,
                                  Unit = "g",
                                  Ingredient = new Ingredient { Id = 1, Name = "beef" }
                              }
                          ]
                      });

        // Act
        var result = await _service.GenerateShoppingListAsync(mealPlan);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalRecipesInPlan);
        Assert.Equal(2, result.TotalRecipesResolved);
        Assert.NotEmpty(result.ShoppingList.Categories);
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync with null meal plan
    /// </summary>
    [Fact]
    public void GenerateShoppingListAsync_WithNullMealPlan_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.GenerateShoppingListAsync(null));
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync with empty recipe ID list
    /// </summary>
    [Fact]
    public async Task GenerateShoppingListAsync_WithEmptyMealPlan_ShouldReturnResult()
    {
        // Arrange
        var mealPlan = new MealPlan();

        // Act
        var result = await _service.GenerateShoppingListAsync(mealPlan);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalRecipesInPlan);
        Assert.Empty(result.ShoppingList.Categories);
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync aggregates equal quantities deterministically
    /// </summary>
    [Fact]
    public async Task GenerateShoppingListAsync_WithRepeatedRecipes_ShouldAggregateQuantities()
    {
        // Arrange
        var mealPlan = new MealPlan { RecipeIds = new List<int> { 1, 1 } };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(1))
            .ReturnsAsync(new Recipe
            {
                Id = 1,
                Name = "Rice",
                SourcePath = "food/starches/rice.md",
                RecipeIngredients =
                [
                    new RecipeIngredient
                    {
                        RecipeId = 1,
                        IngredientId = 10,
                        Amount = 100,
                        Unit = "g",
                        Ingredient = new Ingredient { Id = 10, Name = "rice" }
                    }
                ]
            });

        // Act
        var result = await _service.GenerateShoppingListAsync(mealPlan);

        // Assert
        var starchCategory = Assert.Single(result.ShoppingList.Categories);
        var rice = Assert.Single(starchCategory.Items);
        Assert.Equal("rice", rice.Name);
        Assert.Equal(200, rice.Quantity);
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync marks missing quantities instead of inventing values
    /// </summary>
    [Fact]
    public async Task GenerateShoppingListAsync_WithMissingAmounts_ShouldKeepQuantityUnspecified()
    {
        // Arrange
        var mealPlan = new MealPlan { RecipeIds = new List<int> { 1 } };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(1))
            .ReturnsAsync(new Recipe
            {
                Id = 1,
                Name = "Sauce",
                SourcePath = "food/sauces/sauce.md",
                RecipeIngredients =
                [
                    new RecipeIngredient
                    {
                        RecipeId = 1,
                        IngredientId = 20,
                        Amount = null,
                        Unit = "",
                        Ingredient = new Ingredient { Id = 20, Name = "vinegar" }
                    }
                ]
            });

        // Act
        var result = await _service.GenerateShoppingListAsync(mealPlan);

        // Assert
        var sauceCategory = Assert.Single(result.ShoppingList.Categories);
        var vinegar = Assert.Single(sauceCategory.Items);
        Assert.Null(vinegar.Quantity);
        Assert.Equal(1, vinegar.UnspecifiedQuantityOccurrences);
        Assert.Contains(result.Formatted.Lines, line => line.Contains("quantidade nao especificada", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tests GetRecipeInfoAsync with valid numeric identifier and existing recipe
    /// </summary>
    [Fact]
    public async Task GetRecipeInfoAsync_WithValidNumericIdentifierAndExistingRecipe_ShouldCallRepository()
    {
        // Arrange
        var recipeId = "123";
        var mockRecipe = new Recipe { Id = 123, Name = "Chicken Curry" };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(123))
                      .ReturnsAsync(mockRecipe);

        // Act
        var result = await _service.GetRecipeInfoAsync(recipeId);

        // Assert
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(123), Times.Once);
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GetRecipeInfoAsync with valid textual identifier and existing recipe
    /// </summary>
    [Fact]
    public async Task GetRecipeInfoAsync_WithValidTextIdentifierAndExistingRecipe_ShouldCallRepository()
    {
        // Arrange
        var recipeName = "Chicken Curry";
        var mockRecipes = new List<Recipe> { new Recipe { Id = 123, Name = "Chicken Curry" } };
        _mockRepository.Setup(r => r.FindRecipesWithIngredientsAsync(new[] { recipeName }))
                      .ReturnsAsync(mockRecipes);
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(123))
                      .ReturnsAsync(mockRecipes.First);

        // Act
        var result = await _service.GetRecipeInfoAsync(recipeName);

        // Assert
        _mockRepository.Verify(r => r.FindRecipesWithIngredientsAsync(new[] { recipeName }), Times.Once);
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(123), Times.Once);
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GetRecipeInfoAsync with valid identifier but non-existent recipe
    /// </summary>
    [Fact]
    public async Task GetRecipeInfoAsync_WithValidIdentifierButNonExistentRecipe_ShouldReturnNull()
    {
        // Arrange
        var recipeName = "NonExistent Recipe";
        _mockRepository.Setup(r => r.FindRecipesWithIngredientsAsync(new[] { recipeName }))
                      .ReturnsAsync(new List<Recipe>());

        // Act
        var result = await _service.GetRecipeInfoAsync(recipeName);

        // Assert
        _mockRepository.Verify(r => r.FindRecipesWithIngredientsAsync(new[] { recipeName }), Times.Once);
        Assert.NotNull(result);
        Assert.Equal($"No recipes found with identifier: {recipeName}", result.Error);
    }

    /// <summary>
    /// Tests GetRecipeInfoAsync with null identifier
    /// </summary>
    [Fact]
    public void GetRecipeInfoAsync_WithNullIdentifier_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetRecipeInfoAsync(null));
    }

    /// <summary>
    /// Tests GetRecipeInfoAsync with empty identifier
    /// </summary>
    [Fact]
    public void GetRecipeInfoAsync_WithEmptyIdentifier_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetRecipeInfoAsync(""));
    }
}