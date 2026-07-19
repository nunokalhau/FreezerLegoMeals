using Xunit;
using Moq;
using Domain.DotNet;
using Repository.DotNet;

namespace Services.DotNet.UnitTests;

/// <summary>
/// Unit tests for the MealService in the .NET service layer.
/// Tests business logic with mocked repository dependencies.
/// </summary>
public class MealServiceTests
{
    private readonly Mock<IRecipeRepository> _mockRepository;
    private readonly MealService _service;

    public MealServiceTests()
    {
        _mockRepository = new Mock<IRecipeRepository>();
        _service = new MealService(_mockRepository.Object);
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException when repository is null
    /// </summary>
    [Fact]
    public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MealService(null));
    }

    /// <summary>
    /// Tests SearchRecipesByIngredientsAsync with valid ingredients list
    /// </summary>
    [Fact]
    public async Task SearchRecipesByIngredientsAsync_WithValidIngredients_ShouldCallRepository()
    {
        // Arrange
        var ingredients = new List<string> { "chicken", "onion" };
        var mockRecipes = new List<Recipe>();
        _mockRepository.Setup(r => r.FindRecipesWithIngredientsAsync(ingredients))
                        .ReturnsAsync(mockRecipes);

        // Act
        var result = await _service.SearchRecipesByIngredientsAsync(ingredients);

        // Assert
        _mockRepository.Verify(r => r.FindRecipesWithIngredientsAsync(ingredients), Times.Once);
        Assert.Same(mockRecipes, result);
    }

    /// <summary>
    /// Tests SearchRecipesByIngredientsAsync with null ingredients list
    /// </summary>
    [Fact]
    public void SearchRecipesByIngredientsAsync_WithNullIngredients_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.SearchRecipesByIngredientsAsync(null));
    }

    /// <summary>
    /// Tests GetRecipeByIdAsync with valid recipe ID
    /// </summary>
    [Fact]
    public async Task GetRecipeByIdAsync_WithValidId_ShouldCallRepository()
    {
        // Arrange
        var mockRecipe = new Recipe { Id = 1, Name = "Chicken Curry" };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(1))
                        .ReturnsAsync(mockRecipe);

        // Act
        var result = await _service.GetRecipeByIdAsync(1);

        // Assert
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(1), Times.Once);
        Assert.Same(mockRecipe, result);
    }

    /// <summary>
    /// Tests GetRecipeByIdAsync with non-existent recipe ID
    /// </summary>
    [Fact]
    public async Task GetRecipeByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(999))
                        .ReturnsAsync((Recipe)null);

        // Act
        var result = await _service.GetRecipeByIdAsync(999);

        // Assert
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(999), Times.Once);
        Assert.Null(result);
    }

    /// <summary>
    /// Tests FindMealsWithIngredientsAsync with valid query containing ingredients
    /// </summary>
    [Fact]
    public async Task FindMealsWithIngredientsAsync_WithValidQuery_ShouldCallRepository()
    {
        // Arrange
        var query = "Find meals with chicken and beef";
        var foundIngredients = new List<string> { "chicken", "beef" };
        var mockRecipes = new List<Recipe>();
        _mockRepository.Setup(r => r.FindRecipesWithIngredientsAsync(foundIngredients))
                        .ReturnsAsync(mockRecipes);

        // Act
        var result = await _service.FindMealsWithIngredientsAsync(query);

        // Assert
        _mockRepository.Verify(r => r.FindRecipesWithIngredientsAsync(foundIngredients), Times.Once);
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
    }

    /// <summary>
    /// Tests FindMealsWithIngredientsAsync with query containing no ingredients
    /// </summary>
    [Fact]
    public async Task FindMealsWithIngredientsAsync_WithNoIngredientsFound_ShouldReturnEmptyResult()
    {
        // Arrange
        var query = "Just some random text";
        _mockRepository.Setup(r => r.FindRecipesWithIngredientsAsync(It.IsAny<IEnumerable<string>>()))
                        .ReturnsAsync(new List<Recipe>());

        // Act
        var result = await _service.FindMealsWithIngredientsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
    }

    /// <summary>
    /// Tests FindMealsWithIngredientsAsync with null query
    /// </summary>
    [Fact]
    public void FindMealsWithIngredientsAsync_WithNullQuery_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.FindMealsWithIngredientsAsync(null));
    }

    /// <summary>
    /// Tests FindMealsWithIngredientsAsync with empty query
    /// </summary>
    [Fact]
    public void FindMealsWithIngredientsAsync_WithEmptyQuery_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.FindMealsWithIngredientsAsync(""));
    }

    /// <summary>
    /// Tests GetRecipeDetailsAsync with valid recipe ID
    /// </summary>
    [Fact]
    public async Task GetRecipeDetailsAsync_WithValidId_ShouldReturnRecipeDetails()
    {
        // Arrange
        var mockRecipe = new Recipe 
        { 
            Id = 1, 
            Name = "Chicken Curry",
            SourcePath = "/recipes/chicken_curry.md"
        };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(1))
                        .ReturnsAsync(mockRecipe);

        // Act
        var result = await _service.GetRecipeDetailsAsync(1);

        // Assert
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(1), Times.Once);
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GetRecipeDetailsAsync with non-existent recipe ID
    /// </summary>
    [Fact]
    public async Task GetRecipeDetailsAsync_WithNonExistentId_ShouldReturnError()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(999))
                        .ReturnsAsync((Recipe?)null);

        // Act
        var result = await _service.GetRecipeDetailsAsync(999);

        // Assert
        _mockRepository.Verify(r => r.GetRecipeByIdAsync(999), Times.Once);
        Assert.NotNull(result);
        Assert.Equal("No recipe found with ID 999", result.Error);
    }
}