using Domain.DotNet;
using Repository.DotNet;
using Services.DotNet.Contracts;

namespace Services.DotNet;

/// <summary>
/// Provides business logic for shopping list generation and management.
/// </summary>
public class ShoppingService : IShoppingService
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IShoppingListGenerator _shoppingListGenerator;
    private readonly IShoppingListFormatter _shoppingListFormatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingService"/> class.
    /// </summary>
    /// <param name="recipeRepository">The recipe repository to use for data access.</param>
    /// <param name="shoppingListGenerator">Deterministic shopping list generator.</param>
    /// <param name="shoppingListFormatter">Shopping list formatter.</param>
    public ShoppingService(
        IRecipeRepository recipeRepository,
        IShoppingListGenerator shoppingListGenerator,
        IShoppingListFormatter shoppingListFormatter)
    {
        _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
        _shoppingListGenerator = shoppingListGenerator ?? throw new ArgumentNullException(nameof(shoppingListGenerator));
        _shoppingListFormatter = shoppingListFormatter ?? throw new ArgumentNullException(nameof(shoppingListFormatter));
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

        var recipe = await _recipeRepository.GetRecipeByIdAsync(recipeId);
        if (recipe == null)
            return new List<RecipeIngredient>();

        return recipe.RecipeIngredients;
    }

    /// <summary>
    /// Get ingredients for multiple recipes.
    /// </summary>
    /// <param name="recipeIdentifiers">List of recipe names or IDs</param>
    /// <returns>Dictionary mapping recipe names to their ingredients</returns>
    public async Task<Dictionary<string, IEnumerable<RecipeIngredient>>> GetMultipleRecipeIngredientsAsync(IEnumerable<string> recipeIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(recipeIdentifiers);

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
    /// Generate a deterministic shopping list from a structured meal plan with recipe IDs only.
    /// </summary>
    /// <param name="mealPlan">Structured meal plan with recipe IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deterministic shopping list data and formatted output.</returns>
    public async Task<ShoppingListResponse> GenerateShoppingListAsync(MealPlan mealPlan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mealPlan);

        var generatedList = await _shoppingListGenerator.GenerateAsync(mealPlan, cancellationToken);
        var formattedList = _shoppingListFormatter.Format(generatedList, "pt");
        var resolvedRecipes = generatedList.RecipeIds.Count - generatedList.MissingRecipeIds.Count;
        var message = resolvedRecipes <= 0
            ? "Nenhuma receita valida foi encontrada para o MealPlan informado."
            : $"Lista de compras deterministica gerada a partir de {resolvedRecipes} receita(s).";

        return new ShoppingListResponse
        {
            MealPlan = mealPlan,
            ShoppingList = generatedList,
            Formatted = formattedList,
            TotalRecipesInPlan = mealPlan.RecipeIds.Count,
            TotalRecipesResolved = resolvedRecipes,
            Message = message
        };
    }

    /// <summary>
    /// Get basic information about a specific recipe.
    /// </summary>
    /// <param name="recipeIdentifier">Recipe name or ID</param>
    /// <returns>Recipe information dictionary or null if not found</returns>
    public async Task<RecipeInfoResponse?> GetRecipeInfoAsync(string recipeIdentifier)
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
                return new RecipeInfoResponse
                {
                    Error = $"No recipes found with identifier: {recipeIdentifier}"
                };
            
            recipeId = recipes.First().Id;
        }

        var recipeDetails = await _recipeRepository.GetRecipeByIdAsync(recipeId);
        
        if (recipeDetails != null)
        {
            return new RecipeInfoResponse
            {
                Id = recipeDetails.Id,
                Name = recipeDetails.Name,
                Servings = recipeDetails.Servings ?? 0,
                TimeToPrepare = recipeDetails.TimeToPrepare ?? 0
            };
        }

        return new RecipeInfoResponse
        {
            Error = $"No recipes found with identifier: {recipeIdentifier}"
        };
    }
}
