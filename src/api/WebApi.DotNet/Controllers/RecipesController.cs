using Microsoft.AspNetCore.Mvc;
using Services.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Domain.DotNet;

namespace WebApi.DotNet.Controllers;

/// <summary>
/// Controller for recipe-related operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly IMealService _mealService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipesController"/> class.
    /// </summary>
    /// <param name="mealService">The meal service to use for business logic.</param>
    public RecipesController(IMealService mealService)
    {
        _mealService = mealService ?? throw new ArgumentNullException(nameof(mealService));
    }

    /// <summary>
    /// Search for recipes containing any of the specified ingredients.
    /// </summary>
    /// <param name="request">The search request containing ingredients.</param>
    /// <returns>List of matching recipes.</returns>
    [HttpPost("search")]
    public async Task<ActionResult<SearchRecipesResponse>> SearchRecipesByIngredients([FromBody] SearchRecipesRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        if (!request.Ingredients?.Any() ?? true)
            return BadRequest("At least one ingredient is required");

        var recipes = await _mealService.SearchRecipesByIngredientsAsync(request.Ingredients);
        
        var response = new SearchRecipesResponse
        {
            Recipes = recipes,
            TotalRecipesFound = recipes?.Count() ?? 0
        };

        return Ok(response);
    }

    /// <summary>
    /// Get a specific recipe by ID.
    /// </summary>
    /// <param name="request">The request containing the recipe ID.</param>
    /// <returns>The recipe if found, null otherwise.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<GetRecipeByIdResponse>> GetRecipeById([FromRoute] GetRecipeByIdRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        var recipe = await _mealService.GetRecipeByIdAsync(request.Id);
        
        var response = new GetRecipeByIdResponse
        {
            Recipe = recipe,
            Found = recipe != null
        };

        return Ok(response);
    }

    /// <summary>
    /// Search for recipes containing specified ingredients and return detailed information.
    /// </summary>
    /// <param name="request">The search request with a natural language query.</param>
    /// <returns>Detailed search results.</returns>
    [HttpPost("find-by-ingredients")]
    public async Task<ActionResult<FindMealsWithIngredientsResponse>> FindMealsWithIngredients([FromBody] FindMealsWithIngredientsRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query is required");

        // Note: This returns object, so we can't strongly type it - let's keep as is for now
        var result = await _mealService.FindMealsWithIngredientsAsync(request.Query);
        
        // We'll need to manually map this since the service returns an object
        var response = new FindMealsWithIngredientsResponse
        {
            Query = request.Query,
            // Extracting data from the returned object (this would be better handled with a proper strongly typed approach)
            TotalRecipesFound = (int)((dynamic)result).total_recipes_found,
            SearchTerms = ((dynamic)result).search_terms ?? Enumerable.Empty<string>(),
            Recipes = ((dynamic)result).recipes ?? Enumerable.Empty<Recipe>(),
            Message = ((dynamic)result).message ?? string.Empty
        };

        return Ok(response);
    }

    /// <summary>
    /// Get detailed information about a specific recipe.
    /// </summary>
    /// <param name="request">The request containing the recipe ID.</param>
    /// <returns>Detailed recipe information.</returns>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<GetRecipeDetailsResponse>> GetRecipeDetails([FromRoute] GetRecipeByIdRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        // Note: This returns object, so we can't strongly type it - let's keep as is for now
        var result = await _mealService.GetRecipeDetailsAsync(request.Id);
        
        // We'll need to manually map this since the service returns an object
        var response = new GetRecipeDetailsResponse
        {
            Recipe = ((dynamic)result).recipe,
            Message = ((dynamic)result).message ?? string.Empty,
            Found = ((dynamic)result).error == null,
            Error = ((dynamic)result).error
        };

        return Ok(response);
    }
}