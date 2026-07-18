using Microsoft.AspNetCore.Mvc;
using Services.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Domain.DotNet;

namespace WebApi.DotNet.Controllers;

/// <summary>
/// Controller for shopping list related operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ShoppingController : ControllerBase
{
    private readonly IShoppingService _shoppingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingController"/> class.
    /// </summary>
    /// <param name="shoppingService">The shopping service to use for business logic.</param>
    public ShoppingController(IShoppingService shoppingService)
    {
        _shoppingService = shoppingService ?? throw new ArgumentNullException(nameof(shoppingService));
    }

    /// <summary>
    /// Get ingredients for a specific recipe.
    /// </summary>
    /// <param name="request">The request containing the recipe identifier.</param>
    /// <returns>List of ingredients for the recipe.</returns>
    [HttpGet("ingredients/{identifier}")]
    public async Task<ActionResult<GetRecipeIngredientsResponse>> GetRecipeIngredients([FromRoute] GetRecipeRequest request)
    {
        if (request == null)
            return BadRequest("Request is required");

        if (string.IsNullOrWhiteSpace(request.Identifier))
            return BadRequest("Recipe identifier is required");

        // Note: This returns IEnumerable<RecipeIngredient>, so we should map it properly
        var ingredients = await _shoppingService.GetRecipeIngredientsAsync(request.Identifier);
        
        var response = new GetRecipeIngredientsResponse
        {
            Ingredients = ingredients,
            RecipeName = request.Identifier,  // Placeholder - actual recipe name would need to be determined
            Found = ingredients?.Any() ?? false
        };

        return Ok(response);
    }

    /// <summary>
    /// Get ingredients for multiple recipes.
    /// </summary>
    /// <param name="request">The request containing recipe identifiers.</param>
    /// <returns>Dictionary mapping recipe names to their ingredients.</returns>
    [HttpPost("ingredients")]
    public async Task<ActionResult<GetMultipleRecipeIngredientsResponse>> GetMultipleRecipeIngredients([FromBody] IEnumerable<string> request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        // Note: This returns Dictionary<string, IEnumerable<RecipeIngredient>>
        var ingredients = await _shoppingService.GetMultipleRecipeIngredientsAsync(request);
        
        var response = new GetMultipleRecipeIngredientsResponse
        {
            RecipeIngredients = ingredients,
            TotalRecipes = ingredients?.Count ?? 0,
            Found = ingredients?.Any() ?? false
        };

        return Ok(response);
    }

    /// <summary>
    /// Generate a shopping list from one or more recipes.
    /// </summary>
    /// <param name="request">The request containing recipe identifiers and optional scaling parameters.</param>
    /// <returns>Generated shopping list data.</returns>
    [HttpPost("generate")]
    public async Task<ActionResult<GenerateShoppingListResponse>> GenerateShoppingList([FromBody] GenerateShoppingListRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        if (!request.RecipeIdentifiers?.Any() ?? true)
            return BadRequest("At least one recipe identifier is required");

        // Note: This returns object, so we can't strongly type it - let's keep as is for now
        var result = await _shoppingService.GenerateShoppingListAsync(
            request.RecipeIdentifiers, 
            request.ScaleFactor, 
            request.GroupByCategory);
        
        // We'll need to manually map this since the service returns an object
        var response = new GenerateShoppingListResponse
        {
            ShoppingList = result,
            Message = "Shopping list generated successfully",
            ScaleFactor = request.ScaleFactor,
            GroupByCategory = request.GroupByCategory
        };

        return Ok(response);
    }

    /// <summary>
    /// Get basic information about a specific recipe.
    /// </summary>
    /// <param name="request">The request containing the recipe identifier.</param>
    /// <returns>Basic recipe information.</returns>
    [HttpGet("{identifier}/info")]
    public async Task<ActionResult<GetRecipeInfoResponse>> GetRecipeInfo([FromRoute] GetRecipeRequest request)
    {
        if (request == null)
            return BadRequest("Request is required");

        if (string.IsNullOrWhiteSpace(request.Identifier))
            return BadRequest("Recipe identifier is required");

        // Note: This returns object, so we can't strongly type it - let's keep as is for now
        var result = await _shoppingService.GetRecipeInfoAsync(request.Identifier);
        
        // We'll need to manually map this since the service returns an object
        var response = new GetRecipeInfoResponse
        {
            Info = result,
            Found = result.Error == null,
            Error = result.Error
        };

        return Ok(response);
    }
}