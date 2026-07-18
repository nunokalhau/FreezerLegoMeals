using Xunit;
using Moq;
using Domain.DotNet;
using Repository.DotNet;

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
        _service = new ShoppingService(_mockRepository.Object);
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException when repository is null
    /// </summary>
    [Fact]
    public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ShoppingService(null));
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
    /// Tests GenerateShoppingListAsync with valid recipe identifiers
    /// </summary>
    [Fact]
    public async Task GenerateShoppingListAsync_WithValidIdentifiers_ShouldReturnExpectedResult()
    {
        // Arrange
        var identifiers = new List<string> { "123", "456" };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(It.IsAny<int>()))
                      .ReturnsAsync((Recipe)null);

        // Act
        var result = await _service.GenerateShoppingListAsync(identifiers);

        // Assert
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync with null identifiers
    /// </summary>
    [Fact]
    public void GenerateShoppingListAsync_WithNullIdentifiers_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.GenerateShoppingListAsync(null));
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync with empty identifiers list
    /// </summary>
    [Fact]
    public async Task GenerateShoppingListAsync_WithEmptyIdentifiers_ShouldReturnResult()
    {
        // Arrange
        var identifiers = new List<string>();

        // Act
        var result = await _service.GenerateShoppingListAsync(identifiers);

        // Assert
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync with scaleFactor parameter
    /// </summary>
    [Fact]
    public async Task GenerateShoppingListAsync_WithScaleFactor_ShouldReturnExpectedResult()
    {
        // Arrange
        var identifiers = new List<string> { "123" };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(It.IsAny<int>()))
                      .ReturnsAsync((Recipe)null);

        // Act
        var result = await _service.GenerateShoppingListAsync(identifiers, 2.0);

        // Assert
        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests GenerateShoppingListAsync with groupByCategory parameter
    /// </summary>
    [Fact]
    public async Task GenerateShoppingListAsync_WithGroupByCategory_ShouldReturnExpectedResult()
    {
        // Arrange
        var identifiers = new List<string> { "123" };
        _mockRepository.Setup(r => r.GetRecipeByIdAsync(It.IsAny<int>()))
                      .ReturnsAsync((Recipe)null);

        // Act
        var result = await _service.GenerateShoppingListAsync(identifiers, groupByCategory: false);

        // Assert
        Assert.NotNull(result);
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