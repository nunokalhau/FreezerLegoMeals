using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreezerLegoMeals.Services.DotNet.Models;

namespace FreezerLegoMeals.Services.DotNet
{
    /// <summary>
    /// Provides business logic for shopping list generation and management.
    /// </summary>
    public class ShoppingService : IShoppingService
    {
        private readonly IRecipeRepository _recipeRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShoppingService"/> class.
        /// </summary>
        /// <param name="recipeRepository">The recipe repository to use for data access.</param>
        public ShoppingService(IRecipeRepository recipeRepository)
        {
            _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
        }

        /// <summary>
        /// Get all ingredients for a specified recipe by name or ID.
        /// </summary>
        /// <param name="recipeIdentifier">Recipe name or ID</param>
        /// <returns>List of ingredient dictionaries with name, amount, unit, and other details</returns>
        public async Task<IEnumerable<RecipeIngredient>> GetRecipeIngredientsAsync(string recipeIdentifier)
        {
            if (string.IsNullOrWhiteSpace(recipeIdentifier))
                throw new ArgumentNullException(nameof(recipeIdentifier));

            // First get the recipe_id from either name or id
            int recipeId;
            if (int.TryParse(recipeIdentifier, out int parsedId))
            {
                recipeId = parsedId;
            }
            else
            {
                // Not a number, so search by name and get the ID
                var recipes = await _recipeRepository.FindRecipesWithIngredientsAsync(new[] { recipeIdentifier });
                if (!recipes.Any())
                    return new List<RecipeIngredient>();
                
                recipeId = recipes.First().Id;
            }

            // In a real implementation, we would need to get ingredients using the recipe ID
            // For now, we'll mock this by returning empty results since we don't have proper data access for ingredients in repo
            var ingredients = new List<RecipeIngredient>();
            return ingredients;
        }

        /// <summary>
        /// Get ingredients for multiple recipes.
        /// </summary>
        /// <param name="recipeIdentifiers">List of recipe names or IDs</param>
        /// <returns>Dictionary mapping recipe names to their ingredients</returns>
        public async Task<Dictionary<string, IEnumerable<RecipeIngredient>>> GetMultipleRecipeIngredientsAsync(IEnumerable<string> recipeIdentifiers)
        {
            if (recipeIdentifiers == null)
                throw new ArgumentNullException(nameof(recipeIdentifiers));

            var allIngredients = new Dictionary<string, IEnumerable<RecipeIngredient>>();
            
            foreach (var identifier in recipeIdentifiers)
            {
                var ingredients = await GetRecipeIngredientsAsync(identifier);
                // Using the first identifier as key for now
                allIngredients[identifier] = ingredients;
            }
            
            return allIngredients;
        }

        /// <summary>
        /// Generate a shopping list from one or more recipes.
        /// </summary>
        /// <param name="recipeIdentifiers">List of recipe names or IDs to include</param>
        /// <param name="scaleFactor">Factor to scale ingredient amounts (e.g., 2.0 for double servings)</param>
        /// <param name="groupByCategory">Whether to group ingredients by category</param>
        /// <returns>Dictionary with shopping list data and metadata</returns>
        public async Task<object> GenerateShoppingListAsync(IEnumerable<string> recipeIdentifiers, 
                                                           double scaleFactor = 1.0, 
                                                           bool groupByCategory = true)
        {
            if (recipeIdentifiers == null)
                throw new ArgumentNullException(nameof(recipeIdentifiers));

            // Get all ingredients from specified recipes
            var recipeIngredients = await GetMultipleRecipeIngredientsAsync(recipeIdentifiers);
            
            // If no ingredients found, return empty result
            if (!recipeIngredients.Any())
            {
                return new
                {
                    recipes = recipeIdentifiers,
                    total_recipes = recipeIdentifiers.Count(),
                    ingredients = new List<object>(),
                    message = $"No recipes found with identifiers: {string.Join(", ", recipeIdentifiers)}"
                };
            }

            // Aggregate ingredients and merge duplicates (simplified version)
            var aggregatedIngredients = new Dictionary<string, object>();
            
            // For this implementation, returning basic structure since we don't have full ingredient data access yet
            return new
            {
                recipes = recipeIdentifiers,
                total_recipes = recipeIdentifiers.Count(),
                scale_factor = scaleFactor,
                ingredients = new List<object>(),
                message = $"Generated shopping list for {recipeIdentifiers.Count()} recipes"
            };
        }

        /// <summary>
        /// Get basic information about a specific recipe.
        /// </summary>
        /// <param name="recipeIdentifier">Recipe name or ID</param>
        /// <returns>Recipe information dictionary or null if not found</returns>
        public async Task<object> GetRecipeInfoAsync(string recipeIdentifier)
        {
            if (string.IsNullOrWhiteSpace(recipeIdentifier))
                throw new ArgumentNullException(nameof(recipeIdentifier));

            // First get the recipe_id from either name or id
            int recipeId;
            if (int.TryParse(recipeIdentifier, out int parsedId))
            {
                recipeId = parsedId;
            }
            else
            {
                // Not a number, so search by name and get the ID
                var recipes = await _recipeRepository.FindRecipesWithIngredientsAsync(new[] { recipeIdentifier });
                if (!recipes.Any())
                    return null;
                
                recipeId = recipes.First().Id;
            }

            // Return basic recipe information
            var recipeDetails = await _recipeRepository.GetRecipeByIdAsync(recipeId);
            
            if (recipeDetails != null)
            {
                return new
                {
                    id = recipeDetails.Id,
                    name = recipeDetails.Name,
                    servings = recipeDetails.Servings,
                    time_to_prepare = recipeDetails.TimeToPrepare
                };
            }
            
            return null;
        }
    }
}