using System;
using System.Threading.Tasks;
using Xunit;
using FreezerLegoMeals.Repository.DotNet;

namespace FreezerLegoMeals.Repository.DotNet.UnitTests
{
    /// <summary>
    /// Unit tests for the .NET repository layer.
    /// </summary>
    public class RepositoryLayerTests
    {
        [Fact]
        public void RecipeRepository_Can_Be_Instantiated()
        {
            // Arrange & Act
            var repository = new RecipeRepository();
            
            // Assert
            Assert.NotNull(repository);
        }

        [Fact]
        public void RecipeRepository_Has_Expected_Interface()
        {
            // Arrange 
            var repository = new RecipeRepository();
            
            // Act & Assert - Verify interface compliance
            Assert.IsAssignableFrom<IRecipeRepository>(repository);
        }

        [Fact]
        public void Repository_Constructor_Accepts_Connection_String()
        {
            // Arrange
            const string connectionString = "Data Source=test.db";
            
            // Act
            var repository = new RecipeRepository(connectionString);
            
            // Assert
            Assert.NotNull(repository);
        }

        [Fact]
        public void Repository_Initializes_With_Default_Connection_String()
        {
            // Arrange & Act
            var repository = new RecipeRepository();
            
            // Assert - Should not throw exception
            Assert.NotNull(repository);
        }

        [Fact]
        public async Task GetRecipesAsync_Returns_Empty_List()
        {
            // Arrange
            var repository = new RecipeRepository();
            
            // Act
            var result = await repository.GetRecipesAsync();
            
            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRecipeByIdAsync_Returns_Null()
        {
            // Arrange
            var repository = new RecipeRepository();
            
            // Act
            var result = await repository.GetRecipeByIdAsync(1);
            
            // Assert
            Assert.Null(result);
        }
    }
}